using Microsoft.EntityFrameworkCore;
using StudioCRM.Application.DTOs.Clients;
using StudioCRM.Domain.Entities;
using StudioCRM.Domain.Enums;
using StudioCRM.Infrastructure.Persistence;

namespace StudioCRM.Infrastructure.Services;

// Call only after applying the caller's client access filter and pagination.
internal static class ClientListSummaryReader
{
    internal static async Task EnrichAsync(StudioCRMDbContext db, List<(int Id, ClientListSummaryDto Summary)> clients)
    {
        if (clients.Count == 0) return;
        var ids = clients.Select(c => c.Id).Distinct().ToArray();
        var now = DateTime.UtcNow;
        var packages = await db.ClientPackages.AsNoTracking()
            .Where(p => ids.Contains(p.ClientId) && p.IsActive && p.ClosureDisposition == null)
            .OrderByDescending(p => p.ActivatedAt ?? p.PurchaseDate).ThenByDescending(p => p.Id)
            .Select(p => new ClientPackage
            {
                Id = p.Id, ClientId = p.ClientId, PackageId = p.PackageId, Name = p.Name,
                BillingMode = p.BillingMode, ExpectedBillingType = p.ExpectedBillingType,
                TotalSessions = p.TotalSessions, UsedSessions = p.UsedSessions, PaymentStatus = p.PaymentStatus,
                ValidUntil = p.ValidUntil, PeriodStartsAt = p.PeriodStartsAt, LocationId = p.LocationId,
                IsActive = p.IsActive
            }).ToListAsync();
        var byClient = packages.ToLookup(p => p.ClientId);
        // Preserve the existing Owner list's settlement balance, not payment credit.
        var balances = await db.ClientBalanceTransactions
            .Where(t => ids.Contains(t.ClientId) && t.Type != BalanceTransactionType.PaymentCredit && t.Type != BalanceTransactionType.PaymentReversal)
            .GroupBy(t => t.ClientId).Select(g => new { Id = g.Key, Balance = g.Sum(t => t.Amount) })
            .ToDictionaryAsync(x => x.Id, x => x.Balance);
        foreach (var (id, summary) in clients)
        {
            summary.CurrentBalance = balances.GetValueOrDefault(id);
            summary.ActivePackages = new();
            foreach (var package in byClient[id])
            {
                var mapped = ClientPaymentService.MapPackage(package);
                var sessionBased = package.BillingMode == PackageBillingMode.SessionBased;
                summary.ActivePackages.Add(new()
                {
                    ClientPackageId = mapped.ClientPackageId, PackageId = mapped.PackageId, Name = mapped.PackageName,
                    BillingMode = mapped.BillingMode, PaymentStatus = mapped.PaymentStatus,
                    Status = mapped.PeriodStatus ?? (package.ValidUntil <= now ? "Expired" : "Active"),
                    TotalSessions = sessionBased ? mapped.TotalSessions : null,
                    UsedSessions = sessionBased ? mapped.UsedSessions : null,
                    RemainingSessions = sessionBased ? mapped.RemainingSessions : null,
                    ValidUntil = mapped.ValidUntil, PeriodStartsAt = mapped.PeriodStartsAt, LocationId = mapped.LocationId
                });
            }
            // Legacy flat fields continue to describe the personal/semi package only.
            var personal = byClient[id].FirstOrDefault(p => p.BillingMode == PackageBillingMode.SessionBased && p.ExpectedBillingType != SessionBillingType.Group);
            if (personal == null) continue;
            var selected = summary.ActivePackages.First(p => p.ClientPackageId == personal.Id);
            summary.ActiveClientPackageId = selected.ClientPackageId;
            summary.ActiveClientPackageName = selected.Name;
            summary.ActivePackageTotalSessions = selected.TotalSessions!.Value;
            summary.ActivePackageUsedSessions = selected.UsedSessions!.Value;
            summary.ActivePackageRemainingSessions = selected.RemainingSessions!.Value;
            summary.ActivePackagePaymentStatus = selected.PaymentStatus;
        }
    }
}
