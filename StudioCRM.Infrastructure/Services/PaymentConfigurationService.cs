using Microsoft.EntityFrameworkCore;
using StudioCRM.Application.DTOs.Billing;
using StudioCRM.Application.DTOs.Locations;
using StudioCRM.Application.Interfaces;
using StudioCRM.Domain.Entities;
using StudioCRM.Infrastructure.Persistence;

namespace StudioCRM.Infrastructure.Services;

public class PaymentConfigurationService : IPaymentConfigurationService
{
    private readonly StudioCRMDbContext _context;

    public PaymentConfigurationService(StudioCRMDbContext context)
    {
        _context = context;
    }

    public async Task<PaymentConfigurationDto> GetConfigurationAsync()
    {
        var legalEntityEntities = await _context.LegalEntities
            .OrderBy(x => x.Name)
            .ToListAsync();

        var paymentProviderAccountEntities = await _context.PaymentProviderAccounts
            .Include(x => x.LegalEntity)
            .Include(x => x.Location)
            .OrderBy(x => x.Provider)
            .ThenBy(x => x.DisplayName)
            .ToListAsync();

        var locationEntities = await _context.Locations
            .Include(x => x.LegalEntity)
            .OrderBy(x => x.Name)
            .ToListAsync();

        return new PaymentConfigurationDto
        {
            LegalEntities = legalEntityEntities.Select(MapLegalEntity).ToList(),
            PaymentProviderAccounts = paymentProviderAccountEntities.Select(MapPaymentProviderAccount).ToList(),
            Locations = locationEntities.Select(MapLocation).ToList()
        };
    }

    public async Task<LegalEntityDto> CreateLegalEntityAsync(UpsertLegalEntityRequest request)
    {
        var legalEntity = new LegalEntity
        {
            Name = NormalizeRequiredText(request.Name, "Legal entity name is required."),
            Nip = NormalizeOptionalText(request.Nip),
            Address = NormalizeOptionalText(request.Address),
            Email = NormalizeOptionalText(request.Email),
            Phone = NormalizeOptionalText(request.Phone),
            IsActive = request.IsActive,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        await _context.LegalEntities.AddAsync(legalEntity);
        await _context.SaveChangesAsync();

        return MapLegalEntity(legalEntity);
    }

    public async Task<LegalEntityDto?> UpdateLegalEntityAsync(int id, UpsertLegalEntityRequest request)
    {
        var legalEntity = await _context.LegalEntities.FirstOrDefaultAsync(x => x.Id == id);

        if (legalEntity is null)
            return null;

        legalEntity.Name = NormalizeRequiredText(request.Name, "Legal entity name is required.");
        legalEntity.Nip = NormalizeOptionalText(request.Nip);
        legalEntity.Address = NormalizeOptionalText(request.Address);
        legalEntity.Email = NormalizeOptionalText(request.Email);
        legalEntity.Phone = NormalizeOptionalText(request.Phone);
        legalEntity.IsActive = request.IsActive;
        legalEntity.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        return MapLegalEntity(legalEntity);
    }

    public async Task<PaymentProviderAccountDto> CreatePaymentProviderAccountAsync(
        UpsertPaymentProviderAccountRequest request)
    {
        await EnsurePaymentProviderReferencesAsync(request);

        var account = new PaymentProviderAccount
        {
            LegalEntityId = request.LegalEntityId,
            LocationId = request.LocationId,
            Provider = NormalizeRequiredText(request.Provider, "Payment provider is required."),
            DisplayName = NormalizeRequiredText(request.DisplayName, "Display name is required."),
            MerchantId = NormalizeOptionalText(request.MerchantId),
            PosId = NormalizeOptionalText(request.PosId),
            AccountKey = NormalizeOptionalText(request.AccountKey),
            IsActive = request.IsActive,
            WebhookSecretConfigured = request.WebhookSecretConfigured,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        await _context.PaymentProviderAccounts.AddAsync(account);
        await _context.SaveChangesAsync();

        return await GetPaymentProviderAccountDtoAsync(account.Id)
            ?? throw new InvalidOperationException("Created payment provider account could not be loaded.");
    }

    public async Task<PaymentProviderAccountDto?> UpdatePaymentProviderAccountAsync(
        int id,
        UpsertPaymentProviderAccountRequest request)
    {
        await EnsurePaymentProviderReferencesAsync(request);

        var account = await _context.PaymentProviderAccounts.FirstOrDefaultAsync(x => x.Id == id);

        if (account is null)
            return null;

        account.LegalEntityId = request.LegalEntityId;
        account.LocationId = request.LocationId;
        account.Provider = NormalizeRequiredText(request.Provider, "Payment provider is required.");
        account.DisplayName = NormalizeRequiredText(request.DisplayName, "Display name is required.");
        account.MerchantId = NormalizeOptionalText(request.MerchantId);
        account.PosId = NormalizeOptionalText(request.PosId);
        account.AccountKey = NormalizeOptionalText(request.AccountKey);
        account.IsActive = request.IsActive;
        account.WebhookSecretConfigured = request.WebhookSecretConfigured;
        account.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        return await GetPaymentProviderAccountDtoAsync(account.Id);
    }

    private async Task EnsurePaymentProviderReferencesAsync(UpsertPaymentProviderAccountRequest request)
    {
        var legalEntityExists = await _context.LegalEntities
            .AnyAsync(x => x.Id == request.LegalEntityId);

        if (!legalEntityExists)
            throw new InvalidOperationException("Legal entity does not exist.");

        if (!request.LocationId.HasValue)
            return;

        var location = await _context.Locations
            .FirstOrDefaultAsync(x => x.Id == request.LocationId.Value);

        if (location is null)
            throw new InvalidOperationException("Location does not exist.");

        if (location.LegalEntityId.HasValue && location.LegalEntityId != request.LegalEntityId)
        {
            throw new InvalidOperationException(
                "Payment provider account legal entity must match the location legal entity.");
        }
    }

    private async Task<PaymentProviderAccountDto?> GetPaymentProviderAccountDtoAsync(int id)
    {
        var account = await _context.PaymentProviderAccounts
            .Include(x => x.LegalEntity)
            .Include(x => x.Location)
            .FirstOrDefaultAsync(x => x.Id == id);

        return account is null ? null : MapPaymentProviderAccount(account);
    }

    private static LegalEntityDto MapLegalEntity(LegalEntity legalEntity)
    {
        return new LegalEntityDto
        {
            Id = legalEntity.Id,
            Name = legalEntity.Name,
            Nip = legalEntity.Nip,
            Address = legalEntity.Address,
            Email = legalEntity.Email,
            Phone = legalEntity.Phone,
            IsActive = legalEntity.IsActive,
            CreatedAt = legalEntity.CreatedAt,
            UpdatedAt = legalEntity.UpdatedAt
        };
    }

    private static PaymentProviderAccountDto MapPaymentProviderAccount(PaymentProviderAccount account)
    {
        return new PaymentProviderAccountDto
        {
            Id = account.Id,
            LegalEntityId = account.LegalEntityId,
            LegalEntityName = account.LegalEntity.Name,
            LocationId = account.LocationId,
            LocationName = account.Location?.Name,
            Provider = account.Provider,
            DisplayName = account.DisplayName,
            MerchantId = account.MerchantId,
            PosId = account.PosId,
            AccountKey = account.AccountKey,
            IsActive = account.IsActive,
            WebhookSecretConfigured = account.WebhookSecretConfigured,
            CreatedAt = account.CreatedAt,
            UpdatedAt = account.UpdatedAt
        };
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

    private static string NormalizeRequiredText(string value, string errorMessage)
    {
        var normalized = value.Trim();

        if (string.IsNullOrWhiteSpace(normalized))
            throw new InvalidOperationException(errorMessage);

        return normalized;
    }

    private static string? NormalizeOptionalText(string? value)
    {
        var normalized = value?.Trim();
        return string.IsNullOrWhiteSpace(normalized) ? null : normalized;
    }
}
