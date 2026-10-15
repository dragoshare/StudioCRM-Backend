using System.Diagnostics;
using System.Threading.Channels;
using StudioCRM.Application.Interfaces.Calendar;

namespace StudioCRM.Api.BackgroundServices;

// Microsoft Graph expects a webhook answer within ~3 s. Notifications are queued and processed one by one in
// the background, so the endpoint answers immediately and user requests do not wait for Outlook imports.
public sealed class OutlookWebhookQueue
{
    private const int Capacity = 500;
    private readonly Channel<string> _channel = Channel.CreateBounded<string>(new BoundedChannelOptions(Capacity)
    {
        // Wait mode makes TryWrite return false when full (DropWrite would report success and lose the item).
        FullMode = BoundedChannelFullMode.Wait,
        SingleReader = true
    });

    public bool TryEnqueue(string body) => _channel.Writer.TryWrite(body);

    internal ChannelReader<string> Reader => _channel.Reader;
}

public sealed class OutlookWebhookWorker : BackgroundService
{
    private readonly OutlookWebhookQueue _queue;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<OutlookWebhookWorker> _logger;

    public OutlookWebhookWorker(OutlookWebhookQueue queue, IServiceProvider serviceProvider, ILogger<OutlookWebhookWorker> logger)
    {
        _queue = queue;
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await foreach (var body in _queue.Reader.ReadAllAsync(stoppingToken))
            {
                var stopwatch = Stopwatch.StartNew();
                try
                {
                    using var scope = _serviceProvider.CreateScope();
                    await scope.ServiceProvider.GetRequiredService<IOutlookWebhookService>().HandleNotificationAsync(body);
                    if (stopwatch.ElapsedMilliseconds >= 3000)
                        _logger.LogInformation("Outlook notification processed in {ElapsedMs} ms.", stopwatch.ElapsedMilliseconds);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Outlook notification processing failed after {ElapsedMs} ms.", stopwatch.ElapsedMilliseconds);
                }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
    }
}
