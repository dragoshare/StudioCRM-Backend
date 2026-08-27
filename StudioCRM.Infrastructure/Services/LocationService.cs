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
        var locations = await _context.Locations
            .Include(l => l.LegalEntity)
            .OrderBy(l => l.Name)
            .ToListAsync();

        return locations.Select(MapLocation).ToList();
    }

    public async Task<LocationDto?> GetByIdAsync(int id)
    {
        var location = await _context.Locations
            .Include(l => l.LegalEntity)
            .FirstOrDefaultAsync(l => l.Id == id);

        return location is null ? null : MapLocation(location);
    }

    public async Task<LocationDto> CreateAsync(CreateLocationDto request)
    {
        await EnsureLegalEntityExistsAsync(request.LegalEntityId);

        var location = new Location
        {
            Name = request.Name,
            City = request.City,
            Address = request.Address,
            IsActive = request.IsActive,
            LegalEntityId = request.LegalEntityId,
            PaymentRecipientName = NormalizeOptionalText(request.PaymentRecipientName),
            BankAccountNumber = NormalizeOptionalText(request.BankAccountNumber),
            BlikPhoneNumber = NormalizeOptionalText(request.BlikPhoneNumber),
            TransferTitleTemplate = NormalizeOptionalText(request.TransferTitleTemplate),
            PaymentDescription = NormalizeOptionalText(request.PaymentDescription),
            FiscalReceiptMode = request.FiscalReceiptMode,
            FiscalRegisterName = NormalizeOptionalText(request.FiscalRegisterName),
            FiscalRegisterNumber = NormalizeOptionalText(request.FiscalRegisterNumber),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        await _context.Locations.AddAsync(location);
        await _context.SaveChangesAsync();

        return await GetByIdAsync(location.Id)
            ?? throw new InvalidOperationException("Created location could not be loaded.");
    }

    public async Task<LocationDto?> UpdateAsync(int id, UpdateLocationDto request)
    {
        await EnsureLegalEntityExistsAsync(request.LegalEntityId);

        var location = await _context.Locations.FirstOrDefaultAsync(l => l.Id == id);

        if (location is null)
            return null;

        location.Name = request.Name;
        location.City = request.City;
        location.Address = request.Address;
        location.IsActive = request.IsActive;
        location.LegalEntityId = request.LegalEntityId;
        location.PaymentRecipientName = NormalizeOptionalText(request.PaymentRecipientName);
        location.BankAccountNumber = NormalizeOptionalText(request.BankAccountNumber);
        location.BlikPhoneNumber = NormalizeOptionalText(request.BlikPhoneNumber);
        location.TransferTitleTemplate = NormalizeOptionalText(request.TransferTitleTemplate);
        location.PaymentDescription = NormalizeOptionalText(request.PaymentDescription);
        location.FiscalReceiptMode = request.FiscalReceiptMode;
        location.FiscalRegisterName = NormalizeOptionalText(request.FiscalRegisterName);
        location.FiscalRegisterNumber = NormalizeOptionalText(request.FiscalRegisterNumber);
        location.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        return await GetByIdAsync(location.Id);
    }

    private async Task EnsureLegalEntityExistsAsync(int? legalEntityId)
    {
        if (!legalEntityId.HasValue)
            return;

        var exists = await _context.LegalEntities.AnyAsync(x => x.Id == legalEntityId.Value);

        if (!exists)
            throw new InvalidOperationException("Legal entity does not exist.");
    }

    private static LocationDto MapLocation(Location location)
    {
        return new LocationDto
        {
            Id = location.Id,
            Name = location.Name,
            City = location.City,
            Address = location.Address,
            IsActive = location.IsActive,
            LegalEntityId = location.LegalEntityId,
            LegalEntityName = location.LegalEntity?.Name,
            PaymentRecipientName = location.PaymentRecipientName,
            BankAccountNumber = location.BankAccountNumber,
            BlikPhoneNumber = location.BlikPhoneNumber,
            TransferTitleTemplate = location.TransferTitleTemplate,
            PaymentDescription = location.PaymentDescription,
            FiscalReceiptMode = location.FiscalReceiptMode,
            FiscalRegisterName = location.FiscalRegisterName,
            FiscalRegisterNumber = location.FiscalRegisterNumber,
            CreatedAt = location.CreatedAt
        };
    }

    private static string? NormalizeOptionalText(string? value)
    {
        var normalized = value?.Trim();
        return string.IsNullOrWhiteSpace(normalized) ? null : normalized;
    }
}
