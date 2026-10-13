using Microsoft.EntityFrameworkCore;
using StudioCRM.Application.DTOs.ClientPackages;
using StudioCRM.Domain.Entities;
using StudioCRM.Domain.Enums;
using StudioCRM.Infrastructure.Services;

namespace StudioCRM.Application.ClientPackages.Services;

public partial class ClientPackageService
{
    private async Task<int> CreatePeriodAsync(Client client, Package offer, CreateClientPackageRequest request)
    {
        RequireOwner();
        if (!offer.IsActive || offer.DurationDays < 1 || offer.DurationDays > 3660)
            throw new InvalidOperationException("Active period offer with 1..3660 days required.");
        if (request.TotalSessions.HasValue && request.TotalSessions != 0 || request.ExpectedBillingType.HasValue || request.ValidUntil.HasValue)
            throw new InvalidOperationException("Period packages use StartsOn and offer duration, not training entries, billing type or ValidUntil.");
        if (offer.LocationId.HasValue && offer.LocationId != client.LocationId)
            throw new InvalidOperationException("Package does not belong to the client's location.");
        var price = request.TotalPrice ?? offer.Price;
        if (price < 0) throw new InvalidOperationException("Price cannot be negative.");
        var start = PeriodPackageService.StartOfDay(request.StartsOn ?? PeriodPackageService.Today());
        var end = PeriodPackageService.AddDays(start, offer.DurationDays);
        await _context.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock({74101}, {client.Id})");
        if (await _context.ClientPackages.AnyAsync(p => p.ClientId == client.Id && p.PackageId == offer.Id &&
            p.BillingMode == PackageBillingMode.PeriodBased && p.IsActive && p.ClosureDisposition == null &&
            p.PeriodStartsAt < end && p.ValidUntil > start))
            throw new InvalidOperationException("This service already covers part of the selected period.");
        var now = DateTime.UtcNow;
        var settings = await _settingsService.GetOwnerSettingsAsync();
        var p = new ClientPackage { ClientId = client.Id, PackageId = offer.Id, BillingMode = PackageBillingMode.PeriodBased,
            Name = string.IsNullOrWhiteSpace(request.Name) ? offer.Name : request.Name.Trim(), Currency = offer.Currency,
            LocationId = offer.LocationId ?? client.LocationId, TotalPrice = price, OriginalPrice = price,
            TotalSessions = 0, SessionsPerWeek = 0, ExpectedUnitPrice = 0,
            PeriodStartsAt = start, ValidUntil = end, PeriodDurationDays = offer.DurationDays, PeriodAutoRenewEnabled = request.AutoRenew,
            PurchaseDate = NormalizeDateTime(request.PurchaseDate, now), ActivatedAt = start, ActivatedByUserId = _currentUser.UserId,
            PaymentStatus = price == 0 ? PaymentStatus.Paid : PaymentStatus.Unpaid, PaidAt = price == 0 ? now : null,
            PaymentDueDate = price == 0 ? null : NormalizeNullableDateTime(request.PaymentDueDate) ?? now.Date.AddDays(settings.DefaultPaymentDueDays),
            IsActive = true, RenewalSource = "PeriodManual", RequestedByUserId = _currentUser.UserId };
        client.Status = "Active";
        client.UpdatedAt = now;
        _context.ClientPackages.Add(p);
        await _context.SaveChangesAsync();
        Audit(p, "PeriodServiceAssigned", "{}", "Owner assigned a period service.");
        await _context.SaveChangesAsync();
        return p.Id;
    }
}
