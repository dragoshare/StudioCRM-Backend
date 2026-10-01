using Microsoft.EntityFrameworkCore;
using StudioCRM.Application.DTOs.Billing;
using StudioCRM.Infrastructure.Persistence;

namespace StudioCRM.Infrastructure.Services;

internal static class ClientPackageDeletion
{
    public static async Task DescribeAsync(StudioCRMDbContext context, IReadOnlyCollection<ClientPackageBillingDto> packages)
    {
        if (packages.Count == 0) return;
        var ids = packages.Select(p => p.ClientPackageId).ToArray();
        var facts = await context.ClientPackages.IgnoreQueryFilters().Where(p => ids.Contains(p.Id))
            .Select(p => new
            {
                p.Id,
                HasPayments = context.ClientPayments.Any(x => x.ClientPackageId == p.Id),
                HasBalance = context.ClientBalanceTransactions.Any(x => x.ClientPackageId == p.Id),
                HasCountedSessions = context.SessionParticipants.Any(x => x.ClientPackageId == p.Id && x.IsCountedFromPackage)
            }).ToDictionaryAsync(p => p.Id);
        foreach (var package in packages)
        {
            var fact = facts[package.ClientPackageId];
            package.DeleteBlockReason = ResolveBlockReason(package, fact.HasPayments, fact.HasBalance, fact.HasCountedSessions);
            package.CanDelete = package.DeleteBlockReason is null;
        }
    }

    internal static string? ResolveBlockReason(ClientPackageBillingDto package, bool hasPayments, bool hasBalance, bool hasCountedSessions)
        => package.ClosureDisposition != null ? "ClosedPackage"
            : hasPayments || package.AmountPaid > 0 ? "PaymentHistory"
            : hasBalance ? "BalanceHistory"
            : hasCountedSessions || package.UsedSessions > 0 ? "CountedSessions"
            : null;
}
