using Microsoft.EntityFrameworkCore;
using StudioCRM.Domain.Entities;
using StudioCRM.Domain.Enums;
using StudioCRM.Infrastructure.Persistence;

namespace StudioCRM.Infrastructure.Services;

internal static class SessionAccountingCorrectionManager
{
    public static async Task RevertAsync(StudioCRMDbContext context, Session session)
    {
        var participants = await context.SessionParticipants
            .Where(p => p.SessionId == session.Id)
            .ToListAsync();
        var countedPackages = participants
            .Where(p => p.IsCountedFromPackage && p.ClientPackageId.HasValue)
            .GroupBy(p => p.ClientPackageId!.Value)
            .Select(group => new CountedPackage(group.Key, group.Sum(p => Math.Max(0, p.SessionsCharged))))
            .ToList();
        var plans = new List<PackageRollbackPlan>();

        foreach (var counted in countedPackages)
        {
            var package = await context.ClientPackages.IgnoreQueryFilters()
                .Include(x => x.Client)
                .FirstOrDefaultAsync(x => x.Id == counted.ClientPackageId);
            if (package is null)
                continue;

            if (package.Client.IsDeleted || package.ClosureDisposition != null)
                throw new InvalidOperationException("Session belongs to an archived client or a closed package and requires manual reconciliation before correction.");

            var newUsedSessions = Math.Max(0, package.UsedSessions - counted.SessionsCharged);
            var reopensPackage = package.UsedSessions >= package.TotalSessions &&
                newUsedSessions < package.TotalSessions;
            ClientPackage? renewal = null;

            if (reopensPackage)
            {
                var renewals = await context.ClientPackages
                    .Where(x => x.PreviousClientPackageId == package.Id)
                    .OrderBy(x => x.Id)
                    .ToListAsync();
                if (renewals.Count > 1)
                    throw new InvalidOperationException("Package has multiple renewal cycles and requires manual reconciliation.");

                renewal = renewals.SingleOrDefault();
                if (renewal is not null)
                {
                    if (renewal.ClosureDisposition != null)
                        throw new InvalidOperationException("Renewal package has a closure decision and cannot be corrected automatically.");
                    var renewalWasUsed = renewal.UsedSessions > 0 ||
                        await context.SessionParticipants.AnyAsync(x =>
                            x.ClientPackageId == renewal.Id && x.IsCountedFromPackage);
                    var renewalHasNextCycle = await context.ClientPackages.AnyAsync(x =>
                        x.PreviousClientPackageId == renewal.Id);
                    var renewalHasPendingPayment = await context.ClientPayments.AnyAsync(x =>
                        x.ClientPackageId == renewal.Id &&
                        x.Status == ClientPaymentStatus.PendingConfirmation);

                    if (renewalWasUsed || renewalHasNextCycle)
                        throw new InvalidOperationException(
                            "Session cannot be corrected automatically because the renewed package has already been used.");
                    if (renewalHasPendingPayment)
                        throw new InvalidOperationException(
                            "Session cannot be corrected while the renewed package has a pending payment.");
                }

                if (package.ExpectedBillingType != SessionBillingType.Group)
                {
                    var hasOtherActivePackage = await context.ClientPackages.AnyAsync(x =>
                        x.ClientId == package.ClientId &&
                        x.Id != package.Id &&
                        (renewal == null || x.Id != renewal.Id) &&
                        x.IsActive &&
                        x.ExpectedBillingType != SessionBillingType.Group);
                    if (hasOtherActivePackage)
                        throw new InvalidOperationException(
                            "Session cannot be corrected because the client already has another active package.");
                }
            }

            plans.Add(new PackageRollbackPlan(package, renewal, newUsedSessions, reopensPackage));
        }

        var activeRenewals = plans
            .Select(plan => plan.Renewal)
            .Where(renewal => renewal?.IsActive == true)
            .Select(renewal => renewal!)
            .DistinctBy(renewal => renewal.Id)
            .ToList();
        if (activeRenewals.Count > 0)
        {
            foreach (var renewal in activeRenewals)
                renewal.IsActive = false;

            // Avoid a transient violation of the one-active-package index.
            await context.SaveChangesAsync();
        }

        foreach (var plan in plans)
        {
            plan.Package.UsedSessions = plan.NewUsedSessions;
            if (!plan.ReopensPackage)
                continue;

            plan.Package.IsActive = true;
            if (plan.Package.ExpectedBillingType != SessionBillingType.Group)
            {
                plan.Package.Client.ActivePackageId = plan.Package.PackageId;
                plan.Package.Client.Status = "Active";
                plan.Package.Client.BillingStatus = plan.Package.PaymentStatus.ToString();
                plan.Package.Client.UpdatedAt = DateTime.UtcNow;
            }
        }

        var adjustments = await context.ClientBalanceTransactions
            .Where(t => t.SessionId == session.Id && t.Type == BalanceTransactionType.PackageAdjustment)
            .ToListAsync();
        context.ClientBalanceTransactions.RemoveRange(adjustments);

        foreach (var participant in participants)
        {
            participant.CountsAgainstPackage = false;
            participant.IsCountedFromPackage = false;
            participant.ClientPackageId = null;
            participant.PackageId = null;
            participant.PlannedBillingType = null;
            participant.ActualBillingType = null;
            participant.ExpectedUnitPrice = null;
            participant.ActualUnitPrice = null;
            participant.BalanceDifference = null;
            participant.UpdatedAt = DateTime.UtcNow;
        }

        // Recalculation queries active packages again in the same transaction.
        await context.SaveChangesAsync();
    }

    private sealed record CountedPackage(int ClientPackageId, int SessionsCharged);

    private sealed record PackageRollbackPlan(
        ClientPackage Package,
        ClientPackage? Renewal,
        int NewUsedSessions,
        bool ReopensPackage);
}
