using StudioCRM.Infrastructure.Services;
namespace StudioCRM.Api.BackgroundServices;
public class PeriodPackageWorker(OrganizationJobRunner organizations, ILogger<PeriodPackageWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(5));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try { await organizations.RunAsync((services, ct) => services.GetRequiredService<PeriodPackageService>().ProcessDueAsync(ct), stoppingToken); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { return; }
            catch (Exception ex) { logger.LogError(ex, "Period service processing failed."); }
        }
    }
}
