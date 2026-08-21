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
            .Include(u => u.UserRoles)
                .ThenInclude(ur => ur.Role)
            .FirstOrDefaultAsync(u => u.Email == request.Email);

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

        if (existingUser is not null)
            return await CreateTrainerProfileForExistingOwnerAsync(
                existingUser,
                trainerRole,
                request,
                distinctLocationIds);

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
            Status = request.Status,
            TeamJoinedDate = NormalizeNullableDateTime(request.TeamJoinedDate),
            OutlookCategoryName = NormalizeOutlookCategoryName(request.OutlookCategoryName),
            OutlookCategoryColor = NormalizeOutlookCategoryColor(request.OutlookCategoryColor),
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

    private async Task<TrainerDto> CreateTrainerProfileForExistingOwnerAsync(
        User user,
        Role trainerRole,
        CreateTrainerDto request,
        List<int> distinctLocationIds)
    {
        var isOwner = user.UserRoles.Any(ur => ur.Role.Name == "Owner");

        if (!isOwner)
            throw new InvalidOperationException("User with this email already exists.");

        var existingTrainer = await _context.Trainers
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(t => t.UserId == user.Id);

        if (existingTrainer is not null && !existingTrainer.IsDeleted)
            throw new InvalidOperationException("User already has a trainer profile.");

        var hasTrainerRole = user.UserRoles.Any(ur => ur.RoleId == trainerRole.Id);
        if (!hasTrainerRole)
        {
            await _context.UserRoles.AddAsync(new UserRole
            {
                UserId = user.Id,
                RoleId = trainerRole.Id
            });
        }

        user.FirstName = request.FirstName;
        user.LastName = request.LastName;
        user.IsActive = true;
        user.UpdatedAt = DateTime.UtcNow;

        var trainer = existingTrainer ?? new Trainer
        {
            UserId = user.Id,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = request.CreatedBy
        };

        trainer.Bio = request.Bio;
        trainer.Phone = request.Phone;
        trainer.Status = request.Status;
        trainer.TeamJoinedDate = NormalizeNullableDateTime(request.TeamJoinedDate);
        trainer.OutlookCategoryName = NormalizeOutlookCategoryName(request.OutlookCategoryName);
        trainer.OutlookCategoryColor = NormalizeOutlookCategoryColor(request.OutlookCategoryColor);
        trainer.IsDeleted = false;
        trainer.DeletedAt = null;
        trainer.UpdatedAt = DateTime.UtcNow;

        if (existingTrainer is null)
            await _context.Trainers.AddAsync(trainer);

        await _context.SaveChangesAsync();

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
        trainer.Status = request.Status;
        trainer.TeamJoinedDate = NormalizeNullableDateTime(request.TeamJoinedDate);
        trainer.OutlookCategoryName = NormalizeOutlookCategoryName(request.OutlookCategoryName);
        trainer.OutlookCategoryColor = NormalizeOutlookCategoryColor(request.OutlookCategoryColor);
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
                AvatarUrl = t.User.AvatarUrl,
                Status = t.Status,
                TeamJoinedDate = t.TeamJoinedDate,
                OutlookCategoryName = t.OutlookCategoryName,
                OutlookCategoryColor = t.OutlookCategoryColor,
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
                AvatarUrl = t.User.AvatarUrl,
                Status = t.Status,
                TeamJoinedDate = t.TeamJoinedDate,
                OutlookCategoryName = t.OutlookCategoryName,
                OutlookCategoryColor = t.OutlookCategoryColor,
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

    private static string? NormalizeOutlookCategoryName(string? value)
    {
        var normalized = value?.Trim();

        return string.IsNullOrWhiteSpace(normalized)
            ? null
            : normalized;
    }

    private static DateTime? NormalizeNullableDateTime(DateTime? value)
    {
        if (!value.HasValue)
            return null;

        return value.Value.Kind switch
        {
            DateTimeKind.Utc => value.Value,
            DateTimeKind.Local => value.Value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value.Value, DateTimeKind.Utc)
        };
    }

    private static string? NormalizeOutlookCategoryColor(string? value)
    {
        var normalized = value?.Trim();

        return string.IsNullOrWhiteSpace(normalized)
            ? null
            : normalized;
    }
}
