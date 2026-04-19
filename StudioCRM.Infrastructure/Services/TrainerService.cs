using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using StudioCRM.Application.DTOs.Trainers;
using StudioCRM.Application.Interfaces;
using StudioCRM.Domain.Entities;
using StudioCRM.Infrastructure.Persistence;

namespace StudioCRM.Infrastructure.Services;

public class TrainerService : ITrainerService
{
    private readonly StudioCRMDbContext _context;
    private readonly PasswordHasher<User> _passwordHasher;

    public TrainerService(StudioCRMDbContext context)
    {
        _context = context;
        _passwordHasher = new PasswordHasher<User>();
    }

    public async Task<TrainerDto> CreateAsync(CreateTrainerDto request)
    {
        var existingUser = await _context.Users
            .FirstOrDefaultAsync(u => u.Email == request.Email);

        if (existingUser is not null)
        {
            throw new InvalidOperationException("User with this email already exists.");
        }

        var trainerRole = await _context.Roles
            .FirstOrDefaultAsync(r => r.Name == "Trainer");

        if (trainerRole is null)
        {
            throw new InvalidOperationException("Trainer role does not exist.");
        }

        var user = new User
        {
            Email = request.Email,
            FirstName = request.FirstName,
            LastName = request.LastName,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        user.PasswordHash = _passwordHasher.HashPassword(user, request.Password);

        await _context.Users.AddAsync(user);
        await _context.SaveChangesAsync();

        var userRole = new UserRole
        {
            UserId = user.Id,
            RoleId = trainerRole.Id
        };

        await _context.UserRoles.AddAsync(userRole);

        var trainer = new Trainer
        {
            UserId = user.Id,
            Bio = request.Bio,
            Phone = request.Phone,
            AvatarUrl = request.AvatarUrl,
            Status = request.Status,
            ExperienceYears = request.ExperienceYears,
            HourlyRate = request.HourlyRate,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            CreatedBy = request.CreatedBy
        };

        await _context.Trainers.AddAsync(trainer);
        await _context.SaveChangesAsync();

        return new TrainerDto
        {
            Id = trainer.Id,
            UserId = user.Id,
            Email = user.Email,
            FirstName = user.FirstName,
            LastName = user.LastName,
            FullName = $"{user.FirstName} {user.LastName}",
            Role = "Trainer",
            Bio = trainer.Bio,
            Phone = trainer.Phone,
            AvatarUrl = trainer.AvatarUrl,
            Status = trainer.Status,
            ExperienceYears = trainer.ExperienceYears,
            RatingAverage = 0,
            SessionsCount = 0,
            ActiveClientsCount = 0,
            HourlyRate = trainer.HourlyRate,
            CreatedAt = trainer.CreatedAt,
            UpdatedAt = trainer.UpdatedAt,
            CreatedBy = trainer.CreatedBy
        };
    }

    public async Task<List<TrainerDto>> GetAllAsync()
    {
        return await _context.Trainers
            .Include(t => t.User)
            .Include(t => t.Clients)
            .Include(t => t.Sessions)
            .Select(t => new TrainerDto
            {
                Id = t.Id,
                UserId = t.UserId,
                Email = t.User.Email,
                FirstName = t.User.FirstName,
                LastName = t.User.LastName,
                FullName = t.User.FirstName + " " + t.User.LastName,
                Role = t.User.UserRoles
                    .Select(ur => ur.Role.Name)
                    .FirstOrDefault() ?? "Trainer",
                Bio = t.Bio,
                Phone = t.Phone,
                AvatarUrl = t.AvatarUrl,
                Status = t.Status,
                ExperienceYears = t.ExperienceYears,
                RatingAverage = 0,
                SessionsCount = t.Sessions.Count,
                ActiveClientsCount = t.Clients.Count(c => c.Status == "Active"),
                HourlyRate = t.HourlyRate,
                CreatedAt = t.CreatedAt,
                UpdatedAt = t.UpdatedAt,
                CreatedBy = t.CreatedBy
            })
            .ToListAsync();
    }

    public async Task<TrainerDto?> GetByIdAsync(int id)
    {
        return await _context.Trainers
            .Include(t => t.User)
            .Include(t => t.Clients)
            .Include(t => t.Sessions)
            .Where(t => t.Id == id)
            .Select(t => new TrainerDto
            {
                Id = t.Id,
                UserId = t.UserId,
                Email = t.User.Email,
                FirstName = t.User.FirstName,
                LastName = t.User.LastName,
                FullName = t.User.FirstName + " " + t.User.LastName,
                Role = t.User.UserRoles
                    .Select(ur => ur.Role.Name)
                    .FirstOrDefault() ?? "Trainer",
                Bio = t.Bio,
                Phone = t.Phone,
                AvatarUrl = t.AvatarUrl,
                Status = t.Status,
                ExperienceYears = t.ExperienceYears,
                RatingAverage = 0,
                SessionsCount = t.Sessions.Count,
                ActiveClientsCount = t.Clients.Count(c => c.Status == "Active"),
                HourlyRate = t.HourlyRate,
                CreatedAt = t.CreatedAt,
                UpdatedAt = t.UpdatedAt,
                CreatedBy = t.CreatedBy
            })
            .FirstOrDefaultAsync();
    }
}