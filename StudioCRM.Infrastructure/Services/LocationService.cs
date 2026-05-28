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
                PaymentRecipientName = l.PaymentRecipientName,
                BankAccountNumber = l.BankAccountNumber,
                BlikPhoneNumber = l.BlikPhoneNumber,
                TransferTitleTemplate = l.TransferTitleTemplate,
                PaymentDescription = l.PaymentDescription,
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
                PaymentRecipientName = l.PaymentRecipientName,
                BankAccountNumber = l.BankAccountNumber,
                BlikPhoneNumber = l.BlikPhoneNumber,
                TransferTitleTemplate = l.TransferTitleTemplate,
                PaymentDescription = l.PaymentDescription,
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
            PaymentRecipientName = NormalizeOptionalText(request.PaymentRecipientName),
            BankAccountNumber = NormalizeOptionalText(request.BankAccountNumber),
            BlikPhoneNumber = NormalizeOptionalText(request.BlikPhoneNumber),
            TransferTitleTemplate = NormalizeOptionalText(request.TransferTitleTemplate),
            PaymentDescription = NormalizeOptionalText(request.PaymentDescription),
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
            PaymentRecipientName = location.PaymentRecipientName,
            BankAccountNumber = location.BankAccountNumber,
            BlikPhoneNumber = location.BlikPhoneNumber,
            TransferTitleTemplate = location.TransferTitleTemplate,
            PaymentDescription = location.PaymentDescription,
            CreatedAt = location.CreatedAt
        };
    }

    public async Task<LocationDto?> UpdateAsync(int id, UpdateLocationDto request)
    {
        var location = await _context.Locations.FirstOrDefaultAsync(l => l.Id == id);

        if (location is null)
            return null;

        location.Name = request.Name;
        location.City = request.City;
        location.Address = request.Address;
        location.IsActive = request.IsActive;
        location.PaymentRecipientName = NormalizeOptionalText(request.PaymentRecipientName);
        location.BankAccountNumber = NormalizeOptionalText(request.BankAccountNumber);
        location.BlikPhoneNumber = NormalizeOptionalText(request.BlikPhoneNumber);
        location.TransferTitleTemplate = NormalizeOptionalText(request.TransferTitleTemplate);
        location.PaymentDescription = NormalizeOptionalText(request.PaymentDescription);

        await _context.SaveChangesAsync();

        return new LocationDto
        {
            Id = location.Id,
            Name = location.Name,
            City = location.City,
            Address = location.Address,
            IsActive = location.IsActive,
            PaymentRecipientName = location.PaymentRecipientName,
            BankAccountNumber = location.BankAccountNumber,
            BlikPhoneNumber = location.BlikPhoneNumber,
            TransferTitleTemplate = location.TransferTitleTemplate,
            PaymentDescription = location.PaymentDescription,
            CreatedAt = location.CreatedAt
        };
    }

    private static string? NormalizeOptionalText(string? value)
    {
        var normalized = value?.Trim();
        return string.IsNullOrWhiteSpace(normalized) ? null : normalized;
    }
}
