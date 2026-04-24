using System.Security.Cryptography;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using StudioCRM.Application.DTOs.Invitations;
using StudioCRM.Application.Interfaces;
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

    public InvitationService(
        StudioCRMDbContext context,
        ICurrentUserService currentUser,
        IOptions<AppSettings> appOptions)
    {
        _context = context;
        _currentUser = currentUser;
        _passwordHasher = new PasswordHasher<User>();
        _appSettings = appOptions.Value;
    }

    public async Task<InvitationDto> CreateAsync(CreateInvitationDto request)
    {
        if (!_currentUser.IsOwner || !_currentUser.UserId.HasValue)
            throw new InvalidOperationException("Only owner can create invitations.");

        if (request.Role != "Trainer" && request.Role != "Client")
            throw new InvalidOperationException("Only Trainer and Client invitations are supported.");

        var location = await _context.Locations
            .FirstOrDefaultAsync(l => l.Id == request.LocationId);

        if (location is null)
            throw new InvalidOperationException("Location does not exist.");

        var existingUser = await _context.Users
            .AnyAsync(u => u.Email == request.Email);

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

        var token = GenerateToken();

        var invitation = new Invitation
        {
            Email = request.Email,
            Role = request.Role,
            Token = token,
            LocationId = request.LocationId,
            ExpiresAt = DateTime.UtcNow.AddDays(3),
            IsAccepted = false,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = _currentUser.UserId.Value
        };

        await _context.Invitations.AddAsync(invitation);
        await _context.SaveChangesAsync();

        return MapToDto(invitation, location.Name);
    }

    public async Task<List<InvitationDto>> GetAllAsync(InvitationFilterDto? filter = null)
    {
        var query = _context.Invitations
            .Include(i => i.Location)
            .AsQueryable();

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

        return await query
            .OrderByDescending(i => i.CreatedAt)
            .Select(i => new InvitationDto
            {
                Id = i.Id,
                Email = i.Email,
                Role = i.Role,
                LocationId = i.LocationId,
                LocationName = i.Location.Name,
                Token = i.Token,
                InviteLink = $"{_appSettings.FrontendBaseUrl}/accept-invitation?token={i.Token}",
                ExpiresAt = i.ExpiresAt,
                IsAccepted = i.IsAccepted,
                AcceptedAt = i.AcceptedAt,
                CancelledAt = i.CancelledAt,
                CreatedAt = i.CreatedAt,
                Status = GetStatus(i)
            })
            .ToListAsync();
    }

    public async Task<InvitationDto?> GetByIdAsync(int id)
    {
        var invitation = await _context.Invitations
            .Include(i => i.Location)
            .FirstOrDefaultAsync(i => i.Id == id);

        if (invitation is null)
            return null;

        return MapToDto(invitation, invitation.Location.Name);
    }

    public async Task<ValidateInvitationDto?> ValidateAsync(string token)
    {
        var invitation = await _context.Invitations
            .Include(i => i.Location)
            .FirstOrDefaultAsync(i => i.Token == token);

        if (invitation is null || invitation.IsAccepted || invitation.CancelledAt != null || invitation.ExpiresAt <= DateTime.UtcNow)
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

        if (invitation is null || invitation.IsAccepted || invitation.CancelledAt != null || invitation.ExpiresAt <= DateTime.UtcNow)
            return false;

        var existingUser = await _context.Users
            .AnyAsync(u => u.Email == invitation.Email);

        if (existingUser)
            throw new InvalidOperationException("User with this email already exists.");

        var role = await _context.Roles
            .FirstOrDefaultAsync(r => r.Name == invitation.Role);

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
        {
            var trainer = new Trainer
            {
                UserId = user.Id,
                Status = "Active",
                ExperienceYears = 0,
                HourlyRate = 0,
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
        else if (invitation.Role == "Client")
        {
            var client = new Client
            {
                UserId = user.Id,
                FirstName = request.FirstName,
                LastName = request.LastName,
                Email = invitation.Email,
                LocationId = invitation.LocationId,
                Status = "New",
                BillingStatus = "Pending",
                ProgressPercent = 0,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                CreatedBy = invitation.CreatedBy
            };

            await _context.Clients.AddAsync(client);
            await _context.SaveChangesAsync();
        }

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

        if (invitation.IsAccepted)
            throw new InvalidOperationException("Accepted invitation cannot be resent.");

        if (invitation.CancelledAt != null)
            throw new InvalidOperationException("Cancelled invitation cannot be resent.");

        invitation.Token = GenerateToken();
        invitation.ExpiresAt = DateTime.UtcNow.AddDays(3);

        await _context.SaveChangesAsync();

        return MapToDto(invitation, invitation.Location.Name);
    }

    public async Task<bool> CancelAsync(int id)
    {
        var invitation = await _context.Invitations
            .FirstOrDefaultAsync(i => i.Id == id);

        if (invitation is null)
            return false;

        if (invitation.IsAccepted)
            throw new InvalidOperationException("Accepted invitation cannot be cancelled.");

        if (invitation.CancelledAt is not null)
            return true;

        invitation.CancelledAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        return true;
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