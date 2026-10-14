using Microsoft.EntityFrameworkCore;
using StudioCRM.Application.Common;
using StudioCRM.Domain.Entities;
using StudioCRM.Domain.Enums;
using StudioCRM.Infrastructure.Persistence;

namespace StudioCRM.Infrastructure.Services;

internal static class AgreedPackageAccounting
{
    // Validates only the agreed per-type limits. The total usage comes from the stored package counter,
    // because an imported running package keeps its opening entries outside session participants.
    public static async Task ValidateAsync(StudioCRMDbContext context, ClientPackage package,
        SessionBillingType type, int sessionId, int charge)
    {
        var terms = AgreedSessionTerms.Read(package);
        if (terms is null) return;
        if (terms.Count == 1)
        {
            // A discounted single-type package may still be used for another personal/semi type.
            SessionBalancePolicy.PerSession(terms[0].BillingType, type);
            if (charge <= 0) throw new InvalidOperationException("Client package has no remaining sessions.");
            return;
        }
        var counted = await context.SessionParticipants
            .Where(p => p.ClientPackageId == package.Id && p.IsCountedFromPackage && p.SessionId != sessionId)
            .Select(p => new { p.ActualBillingType, p.SessionsCharged }).ToListAsync();
        if (counted.Any(p => p.ActualBillingType == null || !terms.Any(t => t.BillingType == p.ActualBillingType)))
            throw new InvalidOperationException("Existing sessions do not match the individual package agreement.");
        AgreedSessionTerms.ValidateUsage(terms, type,
            counted.Where(p => p.ActualBillingType == type).Sum(p => p.SessionsCharged), charge);
    }
}
