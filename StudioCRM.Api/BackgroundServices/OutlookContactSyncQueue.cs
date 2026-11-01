using System.Collections.Concurrent;
using System.Threading.Channels;
using StudioCRM.Api.Security;
using StudioCRM.Application.Interfaces.Calendar;

namespace StudioCRM.Api.BackgroundServices;

// New or edited clients appear in trainers' Outlook contacts without the manual "sync contacts" button.
// A client already waiting in the queue is not queued twice; the worker reads its current state when it runs.
public sealed class OutlookContactSyncQueue : IOutlookContactSyncQueue
{
    private readonly Channel<(Guid OrganizationId, int ClientId)> _channel =
        Channel.CreateUnbounded<(Guid, int)>(new UnboundedChannelOptions { SingleReader = true });
    private readonly ConcurrentDictionary<(Guid, int), byte> _pending = new();

    public void Enqueue(Guid organizationId, int clientId)
    {
        if (_pending.TryAdd((organizationId, clientId), 0))
            _channel.Writer.TryWrite((organizationId, clientId));
    }

    internal ChannelReader<(Guid OrganizationId, int ClientId)> Reader => _channel.Reader;

    internal void Started(Guid organizationId, int clientId) => _pending.TryRemove((organizationId, clientId), out _);
}

public sealed class OutlookContactSyncWorker(OutlookContactSyncQueue queue, IServiceProvider serviceProvider, ILogger<OutlookContactSyncWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await foreach (var (organizationId, clientId) in queue.Reader.ReadAllAsync(stoppingToken))
            {
                // Removed before processing, so a change saved while this push runs is queued again.
                queue.Started(organizationId, clientId);
                try
                {
                    using var scope = serviceProvider.CreateScope();
                    // Set the organization before anything resolves the DbContext (it freezes the context).
                    scope.ServiceProvider.GetRequiredService<RequestOrganizationContext>().SetValidated(organizationId);
                    await scope.ServiceProvider.GetRequiredService<IOutlookContactService>().SyncClientToConnectedOutlooksAsync(clientId);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Outlook contact push of client {ClientId} failed.", clientId);
                }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
    }
}
