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

        var email = NormalizeEmail(request.Email);
        var role = NormalizeRole(request.Role);

        if (string.IsNullOrWhiteSpace(email))
            throw new InvalidOperationException("Email is required.");

        if (role != "Trainer" && role != "Client")
            throw new InvalidOperationException("Only Trainer and Client invitations are supported.");

        if (!_currentUser.IsOwner)
        {
            if (!_currentUser.IsTrainer || role != "Client")
                throw new InvalidOperationException("Trainer can invite only clients.");

            await EnsureTrainerCanInviteToLocationAsync(request.LocationId);
        }

        var location = await _context.Locations.FirstOrDefaultAsync(l => l.Id == request.LocationId);
        if (location is null)
            throw new InvalidOperationException("Location does not exist.");

        var existingUser = await _context.Users.AnyAsync(u => u.Email.ToLower() == email);
        if (existingUser)
            throw new InvalidOperationException("User with this email already exists.");

        var existingPendingInvitation = await _context.Invitations
            .AnyAsync(i =>
                i.Email.ToLower() == email &&
                !i.IsAccepted &&
                i.CancelledAt == null &&
                i.ExpiresAt > DateTime.UtcNow);

        if (existingPendingInvitation)
            throw new InvalidOperationException("There is already an active invitation for this email.");

        var invitation = new Invitation
        {
            Email = email,
            Role = role,
            Token = GenerateToken(),
            LocationId = request.LocationId,
            Location = location,
            ExpiresAt = DateTime.UtcNow.AddDays(3),
            IsAccepted = false,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = _currentUser.UserId.Value
        };

        await _context.Invitations.AddAsync(invitation);
        await _context.SaveChangesAsync();

        var inviteLink = $"{_appSettings.FrontendBaseUrl}/accept-invitation?token={invitation.Token}";

        await SendInvitationEmailAndTrackResultAsync(invitation, location.Name, inviteLink);

        return MapToDto(invitation);
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
            {
                var role = NormalizeRole(filter.Role);
                query = query.Where(i => i.Role == role);
            }

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
                var status = filter.Status.Trim();

                query = status switch
                {
                    var value when value.Equals("Pending", StringComparison.OrdinalIgnoreCase) =>
                        query.Where(i => !i.IsAccepted && i.CancelledAt == null && i.ExpiresAt > now),
                    var value when value.Equals("Accepted", StringComparison.OrdinalIgnoreCase) =>
                        query.Where(i => i.IsAccepted),
                    var value when value.Equals("Expired", StringComparison.OrdinalIgnoreCase) =>
                        query.Where(i => !i.IsAccepted && i.CancelledAt == null && i.ExpiresAt <= now),
                    var value when value.Equals("Cancelled", StringComparison.OrdinalIgnoreCase) =>
                        query.Where(i => i.CancelledAt != null),
                    _ => query
                };
            }
        }

        var invitations = await query
            .OrderByDescending(i => i.CreatedAt)
            .ToListAsync();

        return invitations
            .Select(MapToDto)
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

        return MapToDto(invitation);
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
            LocationName = GetLocationName(invitation),
            ExpiresAt = invitation.ExpiresAt
        };
    }

    public async Task<bool> AcceptAsync(AcceptInvitationDto request)
    {
        if (string.IsNullOrWhiteSpace(request.FirstName))
            throw new InvalidOperationException("First name is required.");

        if (string.IsNullOrWhiteSpace(request.LastName))
            throw new InvalidOperationException("Last name is required.");

        if (string.IsNullOrWhiteSpace(request.Password))
            throw new InvalidOperationException("Password is required.");

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

        await using var transaction = await _context.Database.BeginTransactionAsync();

        var user = new User
        {
            Email = invitation.Email,
            FirstName = request.FirstName.Trim(),
            LastName = request.LastName.Trim(),
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

        await transaction.CommitAsync();

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

        await SendInvitationEmailAndTrackResultAsync(invitation, GetLocationName(invitation), inviteLink);

        return MapToDto(invitation);
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
            SubscriptionAutoRenewEnabled = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            CreatedBy = invitation.CreatedBy
        };

        await _context.Clients.AddAsync(client);
        await _context.SaveChangesAsync();
    }

    private InvitationDto MapToDto(Invitation invitation)
    {
        return new InvitationDto
        {
            Id = invitation.Id,
            Email = invitation.Email,
            Role = invitation.Role,
            LocationId = invitation.LocationId,
            LocationName = GetLocationName(invitation),
            Token = invitation.Token,
            InviteLink = $"{_appSettings.FrontendBaseUrl}/accept-invitation?token={invitation.Token}",
            ExpiresAt = invitation.ExpiresAt,
            IsAccepted = invitation.IsAccepted,
            AcceptedAt = invitation.AcceptedAt,
            CancelledAt = invitation.CancelledAt,
            LastSentAt = invitation.LastSentAt,
            LastSendError = invitation.LastSendError,
            CreatedAt = invitation.CreatedAt,
            Status = GetStatus(invitation)
        };
    }

    private async Task SendInvitationEmailAndTrackResultAsync(
        Invitation invitation,
        string locationName,
        string inviteLink)
    {
        try
        {
            await _emailService.SendInvitationEmailAsync(
                invitation.Email,
                invitation.Role,
                locationName,
                inviteLink);

            invitation.LastSentAt = DateTime.UtcNow;
            invitation.LastSendError = null;
            await _context.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            invitation.LastSendError = ex.Message;
            await _context.SaveChangesAsync();

            throw new InvalidOperationException(
                "Invitation was saved, but email could not be sent. Check Resend domain/API key configuration. " +
                ex.Message);
        }
    }

    private static string GetStatus(Invitation invitation)
    {
        if (invitation.IsAccepted) return "Accepted";
        if (invitation.CancelledAt != null) return "Cancelled";
        if (invitation.ExpiresAt <= DateTime.UtcNow) return "Expired";
        return "Pending";
    }

    private static string GetLocationName(Invitation invitation)
    {
        return invitation.Location?.Name ?? string.Empty;
    }

    private static string NormalizeEmail(string? email)
    {
        return (email ?? string.Empty).Trim().ToLowerInvariant();
    }

    private static string NormalizeRole(string? role)
    {
        var value = (role ?? string.Empty).Trim();

        if (value.Equals("trainer", StringComparison.OrdinalIgnoreCase))
            return "Trainer";

        if (value.Equals("client", StringComparison.OrdinalIgnoreCase))
            return "Client";

        return value;
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
