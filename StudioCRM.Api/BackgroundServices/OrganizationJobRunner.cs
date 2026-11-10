using Microsoft.EntityFrameworkCore;
using StudioCRM.Api.Security;
using StudioCRM.Infrastructure.Persistence;
namespace StudioCRM.Api.BackgroundServices;

public sealed class OrganizationJobRunner(IServiceScopeFactory scopes, ILogger<OrganizationJobRunner> logger)
{
    public async Task RunAsync(Func<IServiceProvider, CancellationToken, Task> operation, CancellationToken ct)
    {
        using var catalog = scopes.CreateScope();
        var db = catalog.ServiceProvider.GetRequiredService<StudioCRMDbContext>();
        var ids = await db.Organizations.AsNoTracking().Where(o => o.Status == "Active" ||
            (o.Id == StudioCRMDbContext.LegacyOrganizationId && o.Status == "LegacyActive"))
            .OrderBy(o => o.Id).Select(o => o.Id).ToListAsync(ct);
        foreach (var id in ids)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                using var studio = scopes.CreateScope();
                studio.ServiceProvider.GetRequiredService<RequestOrganizationContext>().SetValidated(id);
                await operation(studio.ServiceProvider, ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
            catch (Exception ex) { logger.LogError(ex, "Background operation failed for organization {OrganizationId}", id); }
        }
    }
}
