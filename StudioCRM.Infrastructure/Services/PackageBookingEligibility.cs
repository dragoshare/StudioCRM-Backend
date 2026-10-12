using Microsoft.EntityFrameworkCore;
using StudioCRM.Domain.Entities;
using StudioCRM.Domain.Enums;
using StudioCRM.Infrastructure.Persistence;
namespace StudioCRM.Infrastructure.Services;
internal static class PackageBookingEligibility
{
    public static async Task<ClientPackage?> ResolveAsync(StudioCRMDbContext db, int clientId, Session session, int entries, int? packageId = null)
    {
        var group = GroupBookingPolicy.IsGroup(session);
        var candidates = await db.ClientPackages.Where(p => p.BillingMode == PackageBillingMode.SessionBased && p.ClientId == clientId && (packageId == null || p.Id == packageId) && p.IsActive && p.ClosureDisposition == null &&
            (p.ExpectedBillingType == SessionBillingType.Group) == group &&
            (p.LocationId == null || p.LocationId == session.LocationId) &&
            (p.ValidUntil == null || p.ValidUntil >= session.StartAt) &&
            (!group || p.PaymentStatus == PaymentStatus.Paid))
            .OrderBy(p => p.ValidUntil ?? DateTime.MaxValue).ThenBy(p => p.PurchaseDate).ToListAsync();
        foreach (var p in candidates)
        {
            var reserved = group ? await db.SessionParticipants.Where(x => x.ClientPackageId == p.Id && !x.IsCountedFromPackage &&
                x.CountsAgainstPackage && x.SessionId != session.Id && x.Session.Status != "Cancelled" && x.Session.Status != "Completed" &&
                x.AttendanceStatus != "CancelledInTime" && x.AttendanceStatus != "CancelledLate").SumAsync(x => x.SessionsCharged) : 0;
            if (p.TotalSessions - p.UsedSessions - reserved >= entries) return p;
        }
        return null;
    }
}
