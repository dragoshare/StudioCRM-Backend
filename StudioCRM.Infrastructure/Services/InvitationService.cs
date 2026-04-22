using System.Security.Cryptography;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using StudioCRM.Application.DTOs.Invitations;
using StudioCRM.Application.Interfaces;
using StudioCRM.Domain.Entities;
using StudioCRM.Infrastructure.Persistence;

namespace StudioCRM.Infrastructure.Services;

public class InvitationService : IInvitationService
{
    private readonly StudioCRMDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly PasswordHasher<User> _passwordHasher;

    public InvitationService(
        StudioCRMDbContext context,
        ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
        _passwordHasher = new PasswordHasher<User>();
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
            .AnyAsync(i => i.Email == request.Email && !i.IsAccepted && i.ExpiresAt > DateTime.UtcNow);

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

        return new InvitationDto
        {
            Id = invitation.Id,
            Email = invitation.Email,
            Role = invitation.Role,
            LocationId = invitation.LocationId,
            LocationName = location.Name,
            Token = invitation.Token,
            InviteLink = $"https://twoj-frontend.pl/accept-invitation?token={invitation.Token}",
            ExpiresAt = invitation.ExpiresAt,
            IsAccepted = invitation.IsAccepted
        };
    }

    public async Task<ValidateInvitationDto?> ValidateAsync(string token)
    {
        var invitation = await _context.Invitations
            .Include(i => i.Location)
            .FirstOrDefaultAsync(i => i.Token == token);

        if (invitation is null || invitation.IsAccepted || invitation.ExpiresAt <= DateTime.UtcNow)
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

        if (invitation is null || invitation.IsAccepted || invitation.ExpiresAt <= DateTime.UtcNow)
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
        await _context.SaveChangesAsync();

        return true;
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