using Microsoft.EntityFrameworkCore;
using StudioCRM.Application.Common;
using StudioCRM.Domain.Entities;
using StudioCRM.Domain.Enums;
using StudioCRM.Infrastructure.Persistence;

namespace StudioCRM.Infrastructure.Services;

internal static class AgreedPackageAccounting
{
    public static async Task<int?> ValidateAsync(StudioCRMDbContext context, ClientPackage package,
        SessionBillingType type, int sessionId, int charge)
    {
        var terms = AgreedSessionTerms.Read(package);
        if (terms is null) return null;
        var counted = await context.SessionParticipants
            .Where(p => p.ClientPackageId == package.Id && p.IsCountedFromPackage && p.SessionId != sessionId)
            .Select(p => new { p.ActualBillingType, p.SessionsCharged }).ToListAsync();
        if (counted.Any(p => p.ActualBillingType == null || !terms.Any(t => t.BillingType == p.ActualBillingType)))
            throw new InvalidOperationException("Existing sessions do not match the individual package agreement.");
        AgreedSessionTerms.ValidateUsage(terms, type,
            counted.Where(p => p.ActualBillingType == type).Sum(p => p.SessionsCharged), charge);
        return terms.Sum(t => t.OpeningUsed) + counted.Sum(p => p.SessionsCharged) + charge;
    }
}
