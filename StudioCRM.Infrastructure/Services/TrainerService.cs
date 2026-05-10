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
            throw new InvalidOperationException("User with this email already exists.");

        if (request.LocationIds is null || request.LocationIds.Count == 0)
            throw new InvalidOperationException("Trainer must be assigned to at least one location.");

        var distinctLocationIds = request.LocationIds.Distinct().ToList();

        var locationsExist = await _context.Locations
            .CountAsync(l => distinctLocationIds.Contains(l.Id));

        if (locationsExist != distinctLocationIds.Count)
            throw new InvalidOperationException("One or more locations do not exist.");

        var trainerRole = await _context.Roles
            .FirstOrDefaultAsync(r => r.Name == "Trainer");

        if (trainerRole is null)
            throw new InvalidOperationException("Trainer role does not exist.");

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
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            CreatedBy = request.CreatedBy
        };

        await _context.Trainers.AddAsync(trainer);
        await _context.SaveChangesAsync();

        await _context.TrainerLocations.AddRangeAsync(
            distinctLocationIds.Select(locationId => new TrainerLocation
            {
                TrainerId = trainer.Id,
                LocationId = locationId
            }));

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
            return null;

        if (request.LocationIds is null || request.LocationIds.Count == 0)
            throw new InvalidOperationException("Trainer must be assigned to at least one location.");

        var distinctLocationIds = request.LocationIds.Distinct().ToList();

        var locationsExist = await _context.Locations
            .CountAsync(l => distinctLocationIds.Contains(l.Id));

        if (locationsExist != distinctLocationIds.Count)
            throw new InvalidOperationException("One or more locations do not exist.");

        trainer.User.FirstName = request.FirstName;
        trainer.User.LastName = request.LastName;
        trainer.User.UpdatedAt = DateTime.UtcNow;

        trainer.Bio = request.Bio;
        trainer.Phone = request.Phone;
        trainer.AvatarUrl = request.AvatarUrl;
        trainer.Status = request.Status;
        trainer.ExperienceYears = request.ExperienceYears;
        trainer.UpdatedAt = DateTime.UtcNow;

        var existingTrainerLocations = await _context.TrainerLocations
            .Where(tl => tl.TrainerId == trainer.Id)
            .ToListAsync();

        _context.TrainerLocations.RemoveRange(existingTrainerLocations);

        await _context.TrainerLocations.AddRangeAsync(
            distinctLocationIds.Select(locationId => new TrainerLocation
            {
                TrainerId = trainer.Id,
                LocationId = locationId
            }));

        await _context.SaveChangesAsync();

        return await GetProjectedById(id);
    }

    public async Task<bool> DeleteAsync(int id)
    {
        var trainer = await _context.Trainers
            .Include(t => t.User)
            .FirstOrDefaultAsync(t => t.Id == id);

        if (trainer is null)
            return false;

        trainer.IsDeleted = true;
        trainer.DeletedAt = DateTime.UtcNow;
        trainer.Status = "Inactive";
        trainer.UpdatedAt = DateTime.UtcNow;
        trainer.User.IsActive = false;
        trainer.User.UpdatedAt = DateTime.UtcNow;

        var clients = await _context.Clients
            .Where(c => c.TrainerId == trainer.Id)
            .ToListAsync();

        foreach (var client in clients)
        {
            client.TrainerId = null;
            client.UpdatedAt = DateTime.UtcNow;
        }

        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> RestoreAsync(int id)
    {
        var trainer = await _context.Trainers
            .IgnoreQueryFilters()
            .Include(t => t.User)
            .FirstOrDefaultAsync(t => t.Id == id);

        if (trainer is null || !trainer.IsDeleted)
            return false;

        trainer.IsDeleted = false;
        trainer.DeletedAt = null;
        trainer.Status = "Active";
        trainer.UpdatedAt = DateTime.UtcNow;
        trainer.User.IsActive = true;
        trainer.User.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<List<TrainerDto>> GetDeletedAsync()
    {
        return await _context.Trainers
            .IgnoreQueryFilters()
            .Include(t => t.User)
            .ThenInclude(u => u.UserRoles)
            .ThenInclude(ur => ur.Role)
            .Include(t => t.Clients)
            .Include(t => t.Sessions)
            .Include(t => t.TrainerLocations)
                .ThenInclude(tl => tl.Location)
            .Where(t => t.IsDeleted)
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
                CreatedAt = t.CreatedAt,
                UpdatedAt = t.UpdatedAt,
                CreatedBy = t.CreatedBy,
                LocationIds = t.TrainerLocations.Select(tl => tl.LocationId).ToList(),
                LocationNames = t.TrainerLocations.Select(tl => tl.Location.Name).ToList()
            })
            .ToListAsync();
    }

    private IQueryable<TrainerDto> BuildTrainerQuery()
    {
        return _context.Trainers
            .Include(t => t.User)
            .ThenInclude(u => u.UserRoles)
            .ThenInclude(ur => ur.Role)
            .Include(t => t.Clients)
            .Include(t => t.Sessions)
            .Include(t => t.TrainerLocations)
                .ThenInclude(tl => tl.Location)
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
                CreatedAt = t.CreatedAt,
                UpdatedAt = t.UpdatedAt,
                CreatedBy = t.CreatedBy,
                LocationIds = t.TrainerLocations.Select(tl => tl.LocationId).ToList(),
                LocationNames = t.TrainerLocations.Select(tl => tl.Location.Name).ToList()
            });
    }

    private async Task<TrainerDto> GetProjectedById(int id)
    {
        return await BuildTrainerQuery().FirstAsync(t => t.Id == id);
    }
}
