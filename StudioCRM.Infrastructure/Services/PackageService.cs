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

    public async Task<List<PackageDto>> GetAllAsync()
    {
        return await _context.Packages
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

    public async Task<PackageDto?> GetByIdAsync(int id)
    {
        return await _context.Packages
            .Where(p => p.Id == id)
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
            .FirstOrDefaultAsync();
    }
}