using StudioCRM.Application.Interfaces;

namespace StudioCRM.Api.BackgroundServices;

public class SessionAutoCompletionWorker : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IConfiguration _configuration;
    private readonly ILogger<SessionAutoCompletionWorker> _logger;

    public SessionAutoCompletionWorker(
        IServiceProvider serviceProvider,
        IConfiguration configuration,
        ILogger<SessionAutoCompletionWorker> logger)
    {
        _serviceProvider = serviceProvider;
        _configuration = configuration;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await DelaySafelyAsync(TimeSpan.FromMinutes(1), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await _serviceProvider.GetRequiredService<OrganizationJobRunner>().RunAsync(async (services, ct) =>
                {
                    var result = await services.GetRequiredService<ISessionAutoCompletionService>().CompleteFinishedSessionsAsync(ct);
                    if (result.CompletedCount > 0 || result.FailedCount > 0)
                        _logger.LogInformation("Session auto-completion: {CompletedCount} completed, {FailedCount} failed.", result.CompletedCount, result.FailedCount);
                }, stoppingToken);

            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Session auto-completion check failed.");
            }

            await DelaySafelyAsync(GetInterval(), stoppingToken);
        }
    }

    private TimeSpan GetInterval()
    {
        var minutes = _configuration.GetValue<int?>("Sessions:AutoCompletionIntervalMinutes") ?? 5;
        return TimeSpan.FromMinutes(Math.Max(1, minutes));
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
