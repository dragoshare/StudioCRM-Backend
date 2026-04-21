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

        await _context.UserRoles.AddAsync(new UserRole
        {
            UserId = user.Id,
            RoleId = trainerRole.Id
        });

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

        return await GetProjectedById(trainer.Id);
    }

    public async Task<List<TrainerDto>> GetAllAsync()
    {
        return await BuildTrainerQuery()
            .OrderByDescending(t => t.CreatedAt)
            .ToListAsync();
    }

    public async Task<TrainerDto?> GetByIdAsync(int id)
    {
        return await BuildTrainerQuery()
            .FirstOrDefaultAsync(t => t.Id == id);
    }

    public async Task<TrainerDto?> UpdateAsync(int id, UpdateTrainerDto request)
    {
        var trainer = await _context.Trainers
            .Include(t => t.User)
            .FirstOrDefaultAsync(t => t.Id == id);

        if (trainer is null)
        {
            return null;
        }

        trainer.User.FirstName = request.FirstName;
        trainer.User.LastName = request.LastName;
        trainer.User.UpdatedAt = DateTime.UtcNow;

        trainer.Bio = request.Bio;
        trainer.Phone = request.Phone;
        trainer.AvatarUrl = request.AvatarUrl;
        trainer.Status = request.Status;
        trainer.ExperienceYears = request.ExperienceYears;
        trainer.HourlyRate = request.HourlyRate;
        trainer.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        return await GetProjectedById(id);
    }

    public async Task<bool> DeleteAsync(int id)
    {
        var trainer = await _context.Trainers
            .Include(t => t.User)
            .FirstOrDefaultAsync(t => t.Id == id);

        if (trainer is null)
        {
            return false;
        }

        _context.Trainers.Remove(trainer);
        _context.Users.Remove(trainer.User);
        await _context.SaveChangesAsync();

        return true;
    }

    private IQueryable<TrainerDto> BuildTrainerQuery()
    {
        return _context.Trainers
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
            });
    }

    private async Task<TrainerDto> GetProjectedById(int id)
    {
        return await BuildTrainerQuery().FirstAsync(t => t.Id == id);
    }
}