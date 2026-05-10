using Microsoft.EntityFrameworkCore;
using StudioCRM.Application.DTOs.Packages;
using StudioCRM.Application.Interfaces;
using StudioCRM.Domain.Entities;
using StudioCRM.Infrastructure.Persistence;

namespace StudioCRM.Infrastructure.Services;

public class PackageService : IPackageService
{
    private readonly StudioCRMDbContext _context;

    public PackageService(StudioCRMDbContext context)
    {
        _context = context;
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
        package.LocationId = request.LocationId;
        package.IsActive = request.IsActive;
        package.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        return MapToDto(package);
    }

    public async Task<bool> DeleteAsync(int id)
    {
        var package = await _context.Packages.FirstOrDefaultAsync(p => p.Id == id);
        if (package is null)
        {
            return false;
        }

        package.IsDeleted = true;
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
            LocationId = package.LocationId,
            LocationName = package.Location?.Name,
            IsActive = package.IsActive,
            CreatedAt = package.CreatedAt,
            UpdatedAt = package.UpdatedAt,
            CreatedBy = package.CreatedBy
        };
    }
}
