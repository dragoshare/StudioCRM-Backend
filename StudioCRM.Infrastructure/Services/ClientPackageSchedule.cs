using Microsoft.EntityFrameworkCore;
using StudioCRM.Infrastructure.Persistence;

namespace StudioCRM.Infrastructure.Services;

internal static class ClientPackageSchedule
{
    // Call only after checking access to the client. Ignore archived-client filters,
    // but explicitly preserve the exclusion of deleted sessions.
    internal static async Task<Dictionary<int, DateTime>> GetNextSessionsAsync(
        StudioCRMDbContext context, int clientId, IEnumerable<int> packageIds)
    {
        var ids = packageIds.Distinct().ToArray();
        if (ids.Length == 0) return new();
        var now = DateTime.UtcNow;
        return await context.SessionParticipants.IgnoreQueryFilters()
            .Where(p => p.ClientId == clientId && p.ClientPackageId.HasValue &&
                ids.Contains(p.ClientPackageId.Value) && !p.Session.IsDeleted &&
                p.Session.Status == "Planned" && p.Session.StartAt >= now &&
                p.AttendanceStatus == "Planned" && !p.IsCountedFromPackage)
            .GroupBy(p => p.ClientPackageId!.Value)
            .Select(g => new { PackageId = g.Key, StartAt = g.Min(p => p.Session.StartAt) })
            .ToDictionaryAsync(x => x.PackageId, x => x.StartAt);
    }
}
