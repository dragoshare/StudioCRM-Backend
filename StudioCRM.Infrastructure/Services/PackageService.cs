using Microsoft.EntityFrameworkCore;
using StudioCRM.Application.DTOs.Clients;
using StudioCRM.Application.DTOs.Packages;
using StudioCRM.Application.Interfaces;
using StudioCRM.Domain.Entities;
using StudioCRM.Infrastructure.Persistence;

namespace StudioCRM.Infrastructure.Services;

public class PackageService : IPackageService
{
    private readonly StudioCRMDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public PackageService(
        StudioCRMDbContext context,
        ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<PackageDto> CreateAsync(CreatePackageDto request)
    {
        if (request.LocationId.HasValue)
        {
            var locationExists = await _context.Locations.AnyAsync(l => l.Id == request.LocationId.Value);
            if (!locationExists)
                throw new InvalidOperationException("Location does not exist.");
        }

        var package = new Package
        {
            Name = request.Name,
            Description = request.Description,
            Price = request.Price,
            Currency = request.Currency,
            SessionsLimit = request.SessionsLimit,
            SessionsPerWeek = request.SessionsPerWeek,
            DurationDays = request.DurationDays,
            BillingType = request.BillingType,
            ParticipantsCount = ResolveParticipantsCount(request.ParticipantsCount, request.BillingType),
            LocationId = request.LocationId,
            IsActive = request.IsActive,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            CreatedBy = request.CreatedBy
        };

        await _context.Packages.AddAsync(package);
        await _context.SaveChangesAsync();

        return MapToDto(package);
    }

    public async Task<List<PackageDto>> GetAllAsync()
    {
        return await _context.Packages
            .Include(p => p.Location)
            .OrderByDescending(p => p.CreatedAt)
            .Select(p => MapToDto(p))
            .ToListAsync();
    }

    public async Task<PackageDto?> GetByIdAsync(int id)
    {
        return await _context.Packages
            .Include(p => p.Location)
            .Where(p => p.Id == id)
            .Select(p => MapToDto(p))
            .FirstOrDefaultAsync();
    }

    public async Task<PackageDto?> UpdateAsync(int id, UpdatePackageDto request)
    {
        var package = await _context.Packages.FirstOrDefaultAsync(p => p.Id == id);
        if (package is null)
        {
            return null;
        }

        if (request.LocationId.HasValue)
        {
            var locationExists = await _context.Locations.AnyAsync(l => l.Id == request.LocationId.Value);
            if (!locationExists)
                throw new InvalidOperationException("Location does not exist.");
        }

        package.Name = request.Name;
        package.Description = request.Description;
        package.Price = request.Price;
        package.Currency = request.Currency;
        package.SessionsLimit = request.SessionsLimit;
        package.SessionsPerWeek = request.SessionsPerWeek;
        package.DurationDays = request.DurationDays;
        package.BillingType = request.BillingType;
        package.ParticipantsCount = ResolveParticipantsCount(request.ParticipantsCount, request.BillingType);
        package.LocationId = request.LocationId;
        package.IsActive = request.IsActive;
        package.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        return MapToDto(package);
    }

    public async Task<bool> DeleteAsync(int id)
    {
        var package = await _context.Packages
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(p => p.Id == id);

        if (package is null)
        {
            return false;
        }

        if (package.IsDeleted)
        {
            return true;
        }

        var isUsedByActiveSubscription = await _context.ClientPackages
            .AnyAsync(cp => cp.PackageId == id && cp.IsActive);

        var isScheduledAsNextPackage = await _context.Clients
            .IgnoreQueryFilters()
            .AnyAsync(c => c.ActivePackageId == id || c.NextPackageId == id);

        if (isUsedByActiveSubscription || isScheduledAsNextPackage)
        {
            throw new InvalidOperationException(
                "Package cannot be deleted while it is used by an active or scheduled subscription. Deactivate it or change clients to another package first.");
        }

        package.IsDeleted = true;
        package.IsActive = false;
        package.DeletedAt = DateTime.UtcNow;
        package.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();
        return true;
    }
    public async Task<bool> RestoreAsync(int id)
    {
        var package = await _context.Packages
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(p => p.Id == id);

        if (package is null || !package.IsDeleted)
        {
            return false;
        }

        package.IsDeleted = false;
        package.DeletedAt = null;
        package.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<List<PackageDto>> GetDeletedAsync()
    {
        return await _context.Packages
            .IgnoreQueryFilters()
            .Include(p => p.Location)
            .Where(p => p.IsDeleted)
            .Select(p => MapToDto(p))
            .ToListAsync();
    }

    public async Task<List<ClientDto>?> GetClientsAsync(int id)
    {
        var packageExists = await _context.Packages.AnyAsync(p => p.Id == id);
        if (!packageExists)
        {
            return null;
        }

        var query = _context.Clients
            .Include(c => c.Trainer)
                .ThenInclude(t => t!.User)
            .Include(c => c.Location)
            .Where(c => c.ActivePackageId == id);

        if (_currentUser.IsTrainer && !_currentUser.IsOwner)
        {
            if (!_currentUser.UserId.HasValue)
            {
                return new List<ClientDto>();
            }

            var trainerId = await _context.Trainers
                .Where(t => t.UserId == _currentUser.UserId.Value)
                .Select(t => (int?)t.Id)
                .FirstOrDefaultAsync();

            if (!trainerId.HasValue)
            {
                return new List<ClientDto>();
            }

            query = query.Where(c => c.TrainerId == trainerId.Value);
        }

        return await query
            .OrderByDescending(c => c.CreatedAt)
            .Select(c => new ClientDto
            {
                Id = c.Id,
                TrainerId = c.TrainerId,
                ActivePackageId = c.ActivePackageId,
                LocationId = c.LocationId,
                LocationName = c.Location.Name,
                FirstName = c.FirstName,
                LastName = c.LastName,
                FullName = c.FirstName + " " + c.LastName,
                Email = c.Email,
                EmailContactUrl = "mailto:" + c.Email,
                PhoneNumber = c.PhoneNumber,
                PhoneContactUrl = c.PhoneNumber != null ? "tel:" + c.PhoneNumber : null,
                AvatarUrl = c.User != null ? c.User.AvatarUrl : null,
                Goal = c.Goal,
                Notes = c.Notes,
                BillingStatus = c.BillingStatus,
                Status = c.Status,
                NextSessionAt = c.NextSessionAt,
                CreatedAt = c.CreatedAt,
                UpdatedAt = c.UpdatedAt,
                CreatedBy = c.CreatedBy,
                TrainerFullName = c.Trainer != null
                    ? c.Trainer.User.FirstName + " " + c.Trainer.User.LastName
                    : null
            })
            .ToListAsync();
    }

    private static PackageDto MapToDto(Package package)
    {
        return new PackageDto
        {
            Id = package.Id,
            Name = package.Name,
            Description = package.Description,
            Price = package.Price,
            Currency = package.Currency,
            SessionsLimit = package.SessionsLimit,
            SessionsPerWeek = package.SessionsPerWeek,
            DurationDays = package.DurationDays,
            BillingType = package.BillingType,
            ParticipantsCount = package.ParticipantsCount,
            LocationId = package.LocationId,
            LocationName = package.Location?.Name,
            IsActive = package.IsActive,
            CreatedAt = package.CreatedAt,
            UpdatedAt = package.UpdatedAt,
            CreatedBy = package.CreatedBy
        };
    }

    private static int ResolveParticipantsCount(int? participantsCount, StudioCRM.Domain.Enums.SessionBillingType billingType)
    {
        var resolved = participantsCount ?? (int)billingType;

        if (resolved < 1 || resolved > 4)
        {
            throw new InvalidOperationException("Participants count must be between 1 and 4.");
        }

        return resolved;
    }
}
