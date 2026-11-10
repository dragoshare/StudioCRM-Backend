using StudioCRM.Application.DTOs.Calendar;
using StudioCRM.Application.Interfaces.Calendar;

namespace StudioCRM.Api.BackgroundServices;

// Safety net for Outlook notifications: the webhook queue lives in memory (a restart or deploy loses it) and Graph
// does not resend a notification the endpoint already accepted. Every few minutes the trainers' calendars from
// yesterday to two weeks ahead are compared with CRM, so a missed change (moved, deleted or added event) arrives
// without a manual reconcile. Import only: it never pushes CRM sessions to Outlook.
public sealed class OutlookAutoReconcileWorker(
    IServiceProvider serviceProvider,
    IConfiguration configuration,
    ILogger<OutlookAutoReconcileWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await DelaySafelyAsync(TimeSpan.FromMinutes(2), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            var minutes = configuration.GetValue<int?>("Outlook:AutoReconcileIntervalMinutes") ?? 10;
            if (minutes > 0 && !configuration.GetValue<bool>("Outlook:EmergencyStop"))
            {
                try
                {
                    await serviceProvider.GetRequiredService<OrganizationJobRunner>().RunAsync(async (services, _) =>
                    {
                        var request = new OutlookReconciliationRequestDto { PastDays = 1, FutureDays = 14, ImportOnly = true };
                        var result = await services.GetRequiredService<IOutlookWebhookService>()
                            .ReconcileAsync(request, new OutlookReconcileActor(0, IsOwner: true, IsTrainer: false));
                        if (result.MissingOutlookEventsMarkedDeleted > 0 || result.Errors.Count > 0)
                            logger.LogInformation(
                                "Outlook auto-reconcile: {Calendars} calendars, {Events} events, {Deleted} missing events marked deleted, errors: {Errors}",
                                result.IntegrationsProcessed, result.OutlookEventsFound, result.MissingOutlookEventsMarkedDeleted,
                                string.Join(" | ", result.Errors));
                    }, stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    return;
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Outlook auto-reconcile failed.");
                }
            }

            await DelaySafelyAsync(TimeSpan.FromMinutes(Math.Max(1, minutes)), stoppingToken);
        }
    }

    private static async Task DelaySafelyAsync(TimeSpan delay, CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(delay, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
    }
}
