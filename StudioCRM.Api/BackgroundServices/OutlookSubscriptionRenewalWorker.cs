using StudioCRM.Application.Interfaces.Calendar;

namespace StudioCRM.Api.BackgroundServices;

public class OutlookSubscriptionRenewalWorker : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<OutlookSubscriptionRenewalWorker> _logger;

    public OutlookSubscriptionRenewalWorker(
        IServiceProvider serviceProvider,
        ILogger<OutlookSubscriptionRenewalWorker> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();

                var subscriptionService = scope.ServiceProvider
                    .GetRequiredService<IOutlookSubscriptionService>();

                await subscriptionService.RenewExpiringSubscriptionsAsync();

                _logger.LogInformation("Outlook subscriptions renewal check completed.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Outlook subscriptions renewal check failed.");
            }

            await Task.Delay(TimeSpan.FromMinutes(15), stoppingToken);
        }
    }
}