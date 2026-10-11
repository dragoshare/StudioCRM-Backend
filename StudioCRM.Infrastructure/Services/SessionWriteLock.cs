using Microsoft.EntityFrameworkCore;
using StudioCRM.Infrastructure.Persistence;
namespace StudioCRM.Infrastructure.Services;

internal static class SessionWriteLock
{
    // Common order for staff writes, auto-completion and public reservations: session, then sorted clients.
    internal static async Task AcquireAsync(StudioCRMDbContext context, int sessionId, IEnumerable<int>? requestedClients = null)
    {
        await context.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(74102, {sessionId})");
        var clients = await context.SessionParticipants.Where(p => p.SessionId == sessionId).Select(p => p.ClientId).ToListAsync();
        var allClients = clients.Concat(requestedClients ?? []).Distinct().OrderBy(x => x).ToArray();
        foreach (var clientId in allClients)
            await context.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(74101, {clientId})");
        var packages = await context.ClientPackages.Where(p => allClients.Contains(p.ClientId) &&
            (p.IsActive || context.SessionParticipants.Any(sp => sp.SessionId == sessionId && sp.ClientPackageId == p.Id)))
            .Select(p => p.Id).OrderBy(id => id).ToListAsync();
        foreach (var packageId in packages)
            await context.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(74121, {packageId})");
    }
}
