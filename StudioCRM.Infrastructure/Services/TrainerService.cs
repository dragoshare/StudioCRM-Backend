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
            PhoneNumber = request.PhoneNumber,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
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
            Specialization = request.Specialization,
            EmploymentType = request.EmploymentType,
            HourlyRate = request.HourlyRate,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
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
            PhoneNumber = user.PhoneNumber,
            Specialization = trainer.Specialization,
            EmploymentType = trainer.EmploymentType,
            HourlyRate = trainer.HourlyRate,
            IsActive = trainer.IsActive
        };
    }

    public async Task<List<TrainerDto>> GetAllAsync()
    {
        return await _context.Trainers
            .Include(t => t.User)
            .Select(t => new TrainerDto
            {
                Id = t.Id,
                UserId = t.UserId,
                Email = t.User.Email,
                FirstName = t.User.FirstName,
                LastName = t.User.LastName,
                PhoneNumber = t.User.PhoneNumber,
                Specialization = t.Specialization,
                EmploymentType = t.EmploymentType,
                HourlyRate = t.HourlyRate,
                IsActive = t.IsActive
            })
            .ToListAsync();
    }

    public async Task<TrainerDto?> GetByIdAsync(int id)
    {
        return await _context.Trainers
            .Include(t => t.User)
            .Where(t => t.Id == id)
            .Select(t => new TrainerDto
            {
                Id = t.Id,
                UserId = t.UserId,
                Email = t.User.Email,
                FirstName = t.User.FirstName,
                LastName = t.User.LastName,
                PhoneNumber = t.User.PhoneNumber,
                Specialization = t.Specialization,
                EmploymentType = t.EmploymentType,
                HourlyRate = t.HourlyRate,
                IsActive = t.IsActive
            })
            .FirstOrDefaultAsync();
    }
}