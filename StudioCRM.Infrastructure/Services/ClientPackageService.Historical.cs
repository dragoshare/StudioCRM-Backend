using System.Data;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using StudioCRM.Application.Common;
using StudioCRM.Application.DTOs.ClientPackages;
using StudioCRM.Domain.Entities;
using StudioCRM.Domain.Enums;

namespace StudioCRM.Application.ClientPackages.Services;

public partial class ClientPackageService
{
    internal const string HistoricalSource = "Historical";

    // Records a finished personal package after the fact. It is created inactive with every entry used, so it never
    // becomes the current package, is never chosen for session counting and is not activated by a later payment
    // (ActivatePackageAfterPaymentIfNeeded skips exhausted packages). It does not apply or change the client balance.
    // Its payment is entered afterwards through the normal staff payment with this clientPackageId.
    public async Task<int> CreateHistoricalAsync(HistoricalClientPackageRequest request)
    {
        RequireOwner(); RequireReason(request.Reason);
        if (request.RequestId == Guid.Empty) throw new InvalidOperationException("Provide a unique RequestId for the historical package.");
        await using var tx = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        var action = "PackageHistorical:" + request.RequestId.ToString("N");
        var payload = JsonSerializer.Serialize(request);
        var existing = await _context.ClientAuditEntries.SingleOrDefaultAsync(x => x.Action == action);
        if (existing != null)
        {
            if (existing.BeforeJson != payload) throw new InvalidOperationException("RequestId was already used for another historical package.");
            return JsonDocument.Parse(existing.AfterJson).RootElement.GetProperty("Id").GetInt32();
        }

        var client = await _context.Clients.SingleOrDefaultAsync(c => c.Id == request.ClientId && !c.IsDeleted)
            ?? throw new InvalidOperationException("Client does not exist.");
        var package = await _context.Packages.SingleOrDefaultAsync(p => p.Id == request.PackageId && !p.IsDeleted)
            ?? throw new InvalidOperationException("Package does not exist.");
        if (package.BillingMode == PackageBillingMode.PeriodBased || package.BillingType == SessionBillingType.Group)
            throw new InvalidOperationException("Historical packages are limited to personal and semi-personal entry packages.");
        if (package.LocationId.HasValue && package.LocationId.Value != client.LocationId)
            throw new InvalidOperationException("Package does not belong to the client's location.");

        var now = DateTime.UtcNow;
        var purchaseDate = NormalizeDateTime(request.PurchaseDate, now);
        if (request.PurchaseDate == default || purchaseDate > now)
            throw new InvalidOperationException("Historical package needs a purchase date in the past.");
        var validUntil = PackageValidity.EndOfStudioDay(NormalizeNullableDateTime(request.ValidUntil) ?? purchaseDate);
        if (validUntil < purchaseDate)
            throw new InvalidOperationException("Validity cannot end before purchase.");
        if (NormalizeNullableDateTime(request.ValidUntil) > now)
            throw new InvalidOperationException("A historical package must already be finished; its validity cannot end in the future.");
        var totalSessions = request.TotalSessions ?? package.SessionsLimit;
        if (totalSessions <= 0) throw new InvalidOperationException("Total sessions must be greater than zero.");
        var price = request.TotalPrice ?? package.Price;
        if (price < 0) throw new InvalidOperationException("Package price cannot be negative.");

        var p = new ClientPackage
        {
            ClientId = client.Id,
            Client = client,
            PackageId = package.Id,
            Name = string.IsNullOrWhiteSpace(request.Name) ? package.Name : request.Name.Trim(),
            TotalSessions = totalSessions,
            UsedSessions = totalSessions,
            SessionsPerWeek = package.SessionsPerWeek > 0 ? package.SessionsPerWeek : 1,
            TotalPrice = price,
            OriginalPrice = price,
            BalanceApplied = 0,
            AmountPaid = 0,
            ExpectedUnitPrice = price / totalSessions,
            Currency = package.Currency,
            LocationId = package.LocationId ?? client.LocationId,
            ExpectedBillingType = package.BillingType,
            PurchaseDate = purchaseDate,
            ValidUntil = validUntil,
            PaymentDueDate = price <= 0 ? null : NormalizeNullableDateTime(request.PaymentDueDate) ?? purchaseDate,
            ActivationMode = ClientPackageActivationMode.Immediately,
            ActivatedAt = purchaseDate,
            ActivatedByUserId = _currentUser.UserId,
            RenewalSource = HistoricalSource,
            IsActive = false
        };
        // Inactive, so RefreshStatus leaves the client's current billing status untouched.
        RefreshStatus(p);
        _context.ClientPackages.Add(p);
        await _context.SaveChangesAsync();
        Audit(p, action, payload, request.Reason);
        await _context.SaveChangesAsync();
        await tx.CommitAsync();
        return p.Id;
    }
}
