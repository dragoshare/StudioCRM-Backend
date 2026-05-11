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
                using var scope = _serviceProvider.CreateScope();

                var completionService = scope.ServiceProvider
                    .GetRequiredService<ISessionAutoCompletionService>();

                var result = await completionService.CompleteFinishedSessionsAsync(stoppingToken);

                if (result.CompletedCount > 0 || result.FailedCount > 0)
                {
                    _logger.LogInformation(
                        "Session auto-completion finished. Completed: {CompletedCount}, skipped: {SkippedCount}, failed: {FailedCount}.",
                        result.CompletedCount,
                        result.SkippedCount,
                        result.FailedCount);
                }
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
