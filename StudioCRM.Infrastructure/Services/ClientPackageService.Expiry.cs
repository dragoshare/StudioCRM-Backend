using Microsoft.EntityFrameworkCore;
using StudioCRM.Application.DTOs.ClientPackages;
using StudioCRM.Domain.Enums;
using StudioCRM.Application.Common;
namespace StudioCRM.Application.ClientPackages.Services;

public partial class ClientPackageService
{
    public async Task<int?> DecideExpiredAsync(int clientId, int clientPackageId, ExpiredPackageDecisionRequest request)
    {
        RequireOwner(); RequireReason(request.Reason);
        if (request.Action is not ("Extend" or "End")) throw new InvalidOperationException("Choose Extend or End.");
        await using var tx = await _context.Database.BeginTransactionAsync();
        await _context.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock({74101}, {clientId})");
        await _context.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock({74121}, {clientPackageId})");
        var p = await LoadManagedAsync(clientId, clientPackageId);
        if (p.BillingMode == PackageBillingMode.PeriodBased)
            throw new InvalidOperationException("Use period renewal or assign a new period instead of a training-package expiry decision.");
        if (Version(p) != request.ExpectedVersion) throw new DbUpdateConcurrencyException("Package changed; refresh preview.");
        if (p.ClosureDisposition != null || !p.IsActive || p.ValidUntil == null || p.ValidUntil >= DateTime.UtcNow)
            throw new InvalidOperationException("Decision requires an expired, active, open package.");
        var before = State(p);
        int? replacementId = null;
        if (request.Action == "Extend")
        {
            if (!request.ValidUntil.HasValue || request.ValidUntil.Value <= DateTime.UtcNow)
                throw new InvalidOperationException("New validity must be in the future.");
            p.ValidUntil = PackageValidity.EndOfStudioDay(NormalizeNullableDateTime(request.ValidUntil));
            Audit(p, "ExpiredPackageExtended", before, request.Reason);
        }
        else
        {
            p.IsActive = false;
            p.ClosureDisposition = p.TotalPrice > p.AmountPaid ? "ClosedWithDebt" : "Closed";
            p.ClosedAt = DateTime.UtcNow; p.ClosureReason = request.Reason.Trim();
            Audit(p, "ExpiredPackageEnded", before, request.Reason);
            await _context.SaveChangesAsync();
            if ((p.BillingMode == StudioCRM.Domain.Enums.PackageBillingMode.SessionBased && p.ExpectedBillingType != SessionBillingType.Group))
            {
                if (p.Client.SubscriptionAutoRenewEnabled && p.Client.RenewalCancellationRequestedAt == null)
                {
                    var nextId = p.Client.NextPackageId ?? p.PackageId;
                    if (!await _context.Packages.AnyAsync(x => x.Id == nextId && x.IsActive && (x.BillingMode == StudioCRM.Domain.Enums.PackageBillingMode.SessionBased && x.BillingType != SessionBillingType.Group)))
                        throw new InvalidOperationException("Next personal package is unavailable.");
                    replacementId = await CreateCoreAsync(new CreateClientPackageRequest { ClientId = clientId, PackageId = nextId, PurchaseDate = DateTime.UtcNow }, false);
                    var next = await _context.ClientPackages.SingleAsync(x => x.Id == replacementId);
                    next.PreviousClientPackageId = p.Id; next.RenewalSource = "OwnerExpiryDecision";
                    p.Client.NextPackageId = null;
                }
                else p.Client.ActivePackageId = null;
            }
        }
        await _context.SaveChangesAsync(); await tx.CommitAsync(); return replacementId;
    }
}
