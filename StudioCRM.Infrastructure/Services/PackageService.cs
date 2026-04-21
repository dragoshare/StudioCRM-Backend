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
        var package = new Package
        {
            Name = request.Name,
            Description = request.Description,
            Price = request.Price,
            Currency = request.Currency,
            SessionsLimit = request.SessionsLimit,
            DurationDays = request.DurationDays,
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
            .OrderByDescending(p => p.CreatedAt)
            .Select(p => MapToDto(p))
            .ToListAsync();
    }

    public async Task<PackageDto?> GetByIdAsync(int id)
    {
        return await _context.Packages
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

        package.Name = request.Name;
        package.Description = request.Description;
        package.Price = request.Price;
        package.Currency = request.Currency;
        package.SessionsLimit = request.SessionsLimit;
        package.DurationDays = request.DurationDays;
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
            .Where(p => p.IsDeleted)
            .Select(p => new PackageDto
            {
                Id = p.Id,
                Name = p.Name,
                Description = p.Description,
                Price = p.Price,
                Currency = p.Currency,
                SessionsLimit = p.SessionsLimit,
                DurationDays = p.DurationDays,
                IsActive = p.IsActive,
                CreatedAt = p.CreatedAt,
                UpdatedAt = p.UpdatedAt,
                CreatedBy = p.CreatedBy
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
            DurationDays = package.DurationDays,
            IsActive = package.IsActive,
            CreatedAt = package.CreatedAt,
            UpdatedAt = package.UpdatedAt,
            CreatedBy = package.CreatedBy
        };
    }
}