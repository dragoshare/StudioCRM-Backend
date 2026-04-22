using Microsoft.EntityFrameworkCore;
using StudioCRM.Application.DTOs.Locations;
using StudioCRM.Application.Interfaces;
using StudioCRM.Domain.Entities;
using StudioCRM.Infrastructure.Persistence;

namespace StudioCRM.Infrastructure.Services;

public class LocationService : ILocationService
{
    private readonly StudioCRMDbContext _context;

    public LocationService(StudioCRMDbContext context)
    {
        _context = context;
    }

    public async Task<List<LocationDto>> GetAllAsync()
    {
        return await _context.Locations
            .OrderBy(l => l.Name)
            .Select(l => new LocationDto
            {
                Id = l.Id,
                Name = l.Name,
                City = l.City,
                Address = l.Address,
                IsActive = l.IsActive,
                CreatedAt = l.CreatedAt
            })
            .ToListAsync();
    }

    public async Task<LocationDto?> GetByIdAsync(int id)
    {
        return await _context.Locations
            .Where(l => l.Id == id)
            .Select(l => new LocationDto
            {
                Id = l.Id,
                Name = l.Name,
                City = l.City,
                Address = l.Address,
                IsActive = l.IsActive,
                CreatedAt = l.CreatedAt
            })
            .FirstOrDefaultAsync();
    }

    public async Task<LocationDto> CreateAsync(CreateLocationDto request)
    {
        var location = new Location
        {
            Name = request.Name,
            City = request.City,
            Address = request.Address,
            IsActive = request.IsActive,
            CreatedAt = DateTime.UtcNow
        };

        await _context.Locations.AddAsync(location);
        await _context.SaveChangesAsync();

        return new LocationDto
        {
            Id = location.Id,
            Name = location.Name,
            City = location.City,
            Address = location.Address,
            IsActive = location.IsActive,
            CreatedAt = location.CreatedAt
        };
    }
}