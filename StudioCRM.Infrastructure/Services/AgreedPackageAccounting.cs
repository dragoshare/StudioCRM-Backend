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
        if (counted.Any(p => p.ActualBillingType == null || AgreedSessionTerms.TermFor(terms, p.ActualBillingType.Value) is null))
            throw new InvalidOperationException("Existing sessions do not match the individual package agreement.");
        // Usage of the pool this session uses, including sessions of other types charged to the same pool (3:1 -> 2:1).
        var pool = AgreedSessionTerms.TermFor(terms, type);
        AgreedSessionTerms.ValidateUsage(terms, type,
            counted.Where(p => AgreedSessionTerms.TermFor(terms, p.ActualBillingType!.Value) == pool).Sum(p => p.SessionsCharged), charge);
    }
}
