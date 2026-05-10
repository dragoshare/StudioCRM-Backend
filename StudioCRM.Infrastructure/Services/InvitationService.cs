using System.Security.Cryptography;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using StudioCRM.Application.DTOs.Invitations;
using StudioCRM.Application.Interfaces;
using StudioCRM.Application.Interfaces.Mail;
using StudioCRM.Application.Settings;
using StudioCRM.Domain.Entities;
using StudioCRM.Infrastructure.Persistence;

namespace StudioCRM.Infrastructure.Services;

public class InvitationService : IInvitationService
{
    private readonly StudioCRMDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly PasswordHasher<User> _passwordHasher;
    private readonly AppSettings _appSettings;
    private readonly IEmailService _emailService;

    public InvitationService(
        StudioCRMDbContext context,
        ICurrentUserService currentUser,
        IOptions<AppSettings> appOptions,
        IEmailService emailService)
    {
        _context = context;
        _currentUser = currentUser;
        _passwordHasher = new PasswordHasher<User>();
        _appSettings = appOptions.Value;
        _emailService = emailService;
    }

    public async Task<InvitationDto> CreateAsync(CreateInvitationDto request)
    {
        if (!_currentUser.UserId.HasValue)
            throw new InvalidOperationException("User is not authenticated.");

        if (request.Role != "Trainer" && request.Role != "Client")
            throw new InvalidOperationException("Only Trainer and Client invitations are supported.");

        if (!_currentUser.IsOwner)
        {
            if (!_currentUser.IsTrainer || request.Role != "Client")
                throw new InvalidOperationException("Trainer can invite only clients.");

            await EnsureTrainerCanInviteToLocationAsync(request.LocationId);
        }

        var location = await _context.Locations.FirstOrDefaultAsync(l => l.Id == request.LocationId);
        if (location is null)
            throw new InvalidOperationException("Location does not exist.");

        var existingUser = await _context.Users.AnyAsync(u => u.Email == request.Email);
        if (existingUser)
            throw new InvalidOperationException("User with this email already exists.");

        var existingPendingInvitation = await _context.Invitations
            .AnyAsync(i =>
                i.Email == request.Email &&
                !i.IsAccepted &&
                i.CancelledAt == null &&
                i.ExpiresAt > DateTime.UtcNow);

        if (existingPendingInvitation)
            throw new InvalidOperationException("There is already an active invitation for this email.");

        var invitation = new Invitation
        {
            Email = request.Email.Trim(),
            Role = request.Role,
            Token = GenerateToken(),
            LocationId = request.LocationId,
            ExpiresAt = DateTime.UtcNow.AddDays(3),
            IsAccepted = false,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = _currentUser.UserId.Value
        };

        await _context.Invitations.AddAsync(invitation);
        await _context.SaveChangesAsync();

        var inviteLink = $"{_appSettings.FrontendBaseUrl}/accept-invitation?token={invitation.Token}";

        await _emailService.SendInvitationEmailAsync(
            invitation.Email,
            invitation.Role,
            location.Name,
            inviteLink);

        return MapToDto(invitation, location.Name);
    }

    public async Task<List<InvitationDto>> GetAllAsync(InvitationFilterDto? filter = null)
    {
        var query = _context.Invitations
            .Include(i => i.Location)
            .AsQueryable();

        if (!_currentUser.IsOwner)
        {
            if (!_currentUser.UserId.HasValue)
                return new List<InvitationDto>();

            query = query.Where(i => i.CreatedBy == _currentUser.UserId.Value);
        }

        if (filter is not null)
        {
            if (!string.IsNullOrWhiteSpace(filter.Role))
                query = query.Where(i => i.Role == filter.Role);

            if (filter.LocationId.HasValue)
                query = query.Where(i => i.LocationId == filter.LocationId.Value);

            if (!string.IsNullOrWhiteSpace(filter.Search))
            {
                var search = filter.Search.ToLower();
                query = query.Where(i => i.Email.ToLower().Contains(search));
            }

            if (!string.IsNullOrWhiteSpace(filter.Status))
            {
                var now = DateTime.UtcNow;

                query = filter.Status switch
                {
                    "Pending" => query.Where(i => !i.IsAccepted && i.CancelledAt == null && i.ExpiresAt > now),
                    "Accepted" => query.Where(i => i.IsAccepted),
                    "Expired" => query.Where(i => !i.IsAccepted && i.CancelledAt == null && i.ExpiresAt <= now),
                    "Cancelled" => query.Where(i => i.CancelledAt != null),
                    _ => query
                };
            }
        }

        var invitations = await query
            .OrderByDescending(i => i.CreatedAt)
            .ToListAsync();

        return invitations
            .Select(i => MapToDto(i, i.Location.Name))
            .ToList();
    }

    public async Task<InvitationDto?> GetByIdAsync(int id)
    {
        var invitation = await _context.Invitations
            .Include(i => i.Location)
            .FirstOrDefaultAsync(i => i.Id == id);

        if (invitation is null)
            return null;

        if (!_currentUser.IsOwner && invitation.CreatedBy != _currentUser.UserId)
            return null;

        return MapToDto(invitation, invitation.Location.Name);
    }

    public async Task<ValidateInvitationDto?> ValidateAsync(string token)
    {
        var invitation = await _context.Invitations
            .Include(i => i.Location)
            .FirstOrDefaultAsync(i => i.Token == token);

        if (invitation is null ||
            invitation.IsAccepted ||
            invitation.CancelledAt != null ||
            invitation.ExpiresAt <= DateTime.UtcNow)
            return null;

        return new ValidateInvitationDto
        {
            Email = invitation.Email,
            Role = invitation.Role,
            LocationId = invitation.LocationId,
            LocationName = invitation.Location.Name,
            ExpiresAt = invitation.ExpiresAt
        };
    }

    public async Task<bool> AcceptAsync(AcceptInvitationDto request)
    {
        var invitation = await _context.Invitations
            .Include(i => i.Location)
            .FirstOrDefaultAsync(i => i.Token == request.Token);

        if (invitation is null ||
            invitation.IsAccepted ||
            invitation.CancelledAt != null ||
            invitation.ExpiresAt <= DateTime.UtcNow)
            return false;

        var existingUser = await _context.Users.AnyAsync(u => u.Email == invitation.Email);
        if (existingUser)
            throw new InvalidOperationException("User with this email already exists.");

        var role = await _context.Roles.FirstOrDefaultAsync(r => r.Name == invitation.Role);
        if (role is null)
            throw new InvalidOperationException("Role does not exist.");

        var user = new User
        {
            Email = invitation.Email,
            FirstName = request.FirstName,
            LastName = request.LastName,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        user.PasswordHash = _passwordHasher.HashPassword(user, request.Password);

        await _context.Users.AddAsync(user);
        await _context.SaveChangesAsync();

        await _context.UserRoles.AddAsync(new UserRole
        {
            UserId = user.Id,
            RoleId = role.Id
        });

        await _context.SaveChangesAsync();

        if (invitation.Role == "Trainer")
            await CreateTrainerProfileAsync(user, invitation);
        else if (invitation.Role == "Client")
            await CreateClientProfileAsync(user, invitation, request);

        invitation.IsAccepted = true;
        invitation.AcceptedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        return true;
    }

    public async Task<InvitationDto?> ResendAsync(int id)
    {
        var invitation = await _context.Invitations
            .Include(i => i.Location)
            .FirstOrDefaultAsync(i => i.Id == id);

        if (invitation is null)
            return null;

        if (!_currentUser.IsOwner && invitation.CreatedBy != _currentUser.UserId)
            return null;

        if (invitation.IsAccepted)
            throw new InvalidOperationException("Accepted invitation cannot be resent.");

        if (invitation.CancelledAt != null)
            throw new InvalidOperationException("Cancelled invitation cannot be resent.");

        invitation.Token = GenerateToken();
        invitation.ExpiresAt = DateTime.UtcNow.AddDays(3);

        await _context.SaveChangesAsync();

        var inviteLink = $"{_appSettings.FrontendBaseUrl}/accept-invitation?token={invitation.Token}";

        await _emailService.SendInvitationEmailAsync(
            invitation.Email,
            invitation.Role,
            invitation.Location.Name,
            inviteLink);

        return MapToDto(invitation, invitation.Location.Name);
    }

    public async Task<bool> CancelAsync(int id)
    {
        var invitation = await _context.Invitations.FirstOrDefaultAsync(i => i.Id == id);

        if (invitation is null)
            return false;

        if (!_currentUser.IsOwner && invitation.CreatedBy != _currentUser.UserId)
            return false;

        if (invitation.IsAccepted)
            throw new InvalidOperationException("Accepted invitation cannot be cancelled.");

        if (invitation.CancelledAt is not null)
            return true;

        invitation.CancelledAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        return true;
    }

    private async Task EnsureTrainerCanInviteToLocationAsync(int locationId)
    {
        var trainer = await _context.Trainers
            .Include(t => t.TrainerLocations)
            .FirstOrDefaultAsync(t => t.UserId == _currentUser.UserId && !t.IsDeleted);

        if (trainer is null)
            throw new InvalidOperationException("Trainer profile not found.");

        var hasLocationAccess = trainer.TrainerLocations.Any(tl => tl.LocationId == locationId);
        if (!hasLocationAccess)
            throw new InvalidOperationException("Trainer cannot invite clients to this location.");
    }

    private async Task CreateTrainerProfileAsync(User user, Invitation invitation)
    {
        var trainer = new Trainer
        {
            UserId = user.Id,
            Status = "Active",
            ExperienceYears = 0,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            CreatedBy = invitation.CreatedBy
        };

        await _context.Trainers.AddAsync(trainer);
        await _context.SaveChangesAsync();

        await _context.TrainerLocations.AddAsync(new TrainerLocation
        {
            TrainerId = trainer.Id,
            LocationId = invitation.LocationId
        });

        await _context.SaveChangesAsync();
    }

    private async Task CreateClientProfileAsync(
        User user,
        Invitation invitation,
        AcceptInvitationDto request)
    {
        var client = new Client
        {
            UserId = user.Id,
            TrainerId = null,
            FirstName = request.FirstName,
            LastName = request.LastName,
            Email = invitation.Email,
            LocationId = invitation.LocationId,
            Status = "New",
            BillingStatus = "Pending",
            ProgressPercent = 0,
            SubscriptionAutoRenewEnabled = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            CreatedBy = invitation.CreatedBy
        };

        await _context.Clients.AddAsync(client);
        await _context.SaveChangesAsync();
    }

    private InvitationDto MapToDto(Invitation invitation, string locationName)
    {
        return new InvitationDto
        {
            Id = invitation.Id,
            Email = invitation.Email,
            Role = invitation.Role,
            LocationId = invitation.LocationId,
            LocationName = locationName,
            Token = invitation.Token,
            InviteLink = $"{_appSettings.FrontendBaseUrl}/accept-invitation?token={invitation.Token}",
            ExpiresAt = invitation.ExpiresAt,
            IsAccepted = invitation.IsAccepted,
            AcceptedAt = invitation.AcceptedAt,
            CancelledAt = invitation.CancelledAt,
            CreatedAt = invitation.CreatedAt,
            Status = GetStatus(invitation)
        };
    }

    private static string GetStatus(Invitation invitation)
    {
        if (invitation.IsAccepted) return "Accepted";
        if (invitation.CancelledAt != null) return "Cancelled";
        if (invitation.ExpiresAt <= DateTime.UtcNow) return "Expired";
        return "Pending";
    }

    private static string GenerateToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(48);
        return Convert.ToBase64String(bytes)
            .Replace("+", "-")
            .Replace("/", "_")
            .Replace("=", "");
    }
}
