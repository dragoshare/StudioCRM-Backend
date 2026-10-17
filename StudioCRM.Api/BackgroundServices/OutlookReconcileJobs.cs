using System.Collections.Concurrent;
using System.Diagnostics;
using System.Threading.Channels;
using StudioCRM.Api.Security;
using StudioCRM.Application.DTOs.Calendar;
using StudioCRM.Application.Interfaces.Calendar;

namespace StudioCRM.Api.BackgroundServices;

// A reconcile of every trainer's Outlook can take minutes, longer than the browser/proxy waits. Requests start a
// background job; the endpoint waits briefly and otherwise returns 202 with a job id the frontend can poll.
// One job per organization and user runs at a time: clicking again while it runs returns the running job.
public sealed class OutlookReconcileJobs
{
    private const int KeepFinished = 100;
    private readonly Channel<OutlookReconcileJob> _channel = Channel.CreateUnbounded<OutlookReconcileJob>(new UnboundedChannelOptions { SingleReader = true });
    private readonly ConcurrentDictionary<Guid, OutlookReconcileJob> _jobs = new();
    private readonly object _gate = new();

    public OutlookReconcileJob Enqueue(Guid organizationId, OutlookReconcileActor actor, OutlookReconciliationRequestDto request)
    {
        lock (_gate)
        {
            var running = _jobs.Values.FirstOrDefault(job => job.OrganizationId == organizationId &&
                job.Actor.UserId == actor.UserId && job.State is OutlookReconcileJobState.Queued or OutlookReconcileJobState.Running);
            if (running != null) return running;

            foreach (var old in _jobs.Values.Where(job => job.FinishedAt.HasValue).OrderByDescending(job => job.FinishedAt).Skip(KeepFinished).ToList())
                _jobs.TryRemove(old.Id, out _);

            var created = new OutlookReconcileJob(Guid.NewGuid(), organizationId, actor, request);
            _jobs[created.Id] = created;
            _channel.Writer.TryWrite(created);
            return created;
        }
    }

    public OutlookReconcileJob? Find(Guid id) => _jobs.TryGetValue(id, out var job) ? job : null;

    internal ChannelReader<OutlookReconcileJob> Reader => _channel.Reader;
}

public enum OutlookReconcileJobState { Queued, Running, Completed, Failed }

public sealed class OutlookReconcileJob(Guid id, Guid organizationId, OutlookReconcileActor actor, OutlookReconciliationRequestDto request)
{
    private readonly TaskCompletionSource _done = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public Guid Id { get; } = id;
    public Guid OrganizationId { get; } = organizationId;
    public OutlookReconcileActor Actor { get; } = actor;
    public OutlookReconciliationRequestDto Request { get; } = request;
    public OutlookReconcileJobState State { get; private set; } = OutlookReconcileJobState.Queued;
    public OutlookReconciliationResultDto? Result { get; private set; }
    public string? Error { get; private set; }
    // True when the failure is a business rule (shown to the user as is), false for unexpected errors.
    public bool IsUserError { get; private set; }
    public DateTime QueuedAt { get; } = DateTime.UtcNow;
    public DateTime? StartedAt { get; private set; }
    public DateTime? FinishedAt { get; private set; }
    public Task Done => _done.Task;

    internal void Start() { State = OutlookReconcileJobState.Running; StartedAt = DateTime.UtcNow; }
    internal void Complete(OutlookReconciliationResultDto result) { Result = result; Finish(OutlookReconcileJobState.Completed); }
    internal void Fail(string error, bool isUserError) { Error = error; IsUserError = isUserError; Finish(OutlookReconcileJobState.Failed); }

    private void Finish(OutlookReconcileJobState state)
    {
        State = state;
        FinishedAt = DateTime.UtcNow;
        _done.TrySetResult();
    }
}

public sealed class OutlookReconcileWorker(OutlookReconcileJobs jobs, IServiceProvider serviceProvider, ILogger<OutlookReconcileWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await foreach (var job in jobs.Reader.ReadAllAsync(stoppingToken))
            {
                var stopwatch = Stopwatch.StartNew();
                job.Start();
                try
                {
                    using var scope = serviceProvider.CreateScope();
                    // Set the organization before anything resolves the DbContext (it freezes the context).
                    scope.ServiceProvider.GetRequiredService<RequestOrganizationContext>().SetValidated(job.OrganizationId);
                    var result = await scope.ServiceProvider.GetRequiredService<IOutlookWebhookService>().ReconcileAsync(job.Request, job.Actor);
                    job.Complete(result);
                    logger.LogInformation("Outlook reconcile {JobId} finished in {ElapsedMs} ms ({Integrations} calendars, {Events} events).",
                        job.Id, stopwatch.ElapsedMilliseconds, result.IntegrationsProcessed, result.OutlookEventsFound);
                }
                catch (Exception ex) when (ex is InvalidOperationException or UnauthorizedAccessException or KeyNotFoundException)
                {
                    job.Fail(ex.Message, true);
                    logger.LogWarning("Outlook reconcile {JobId} rejected after {ElapsedMs} ms: {Message}", job.Id, stopwatch.ElapsedMilliseconds, ex.Message);
                }
                catch (Exception ex)
                {
                    job.Fail("Synchronizacja z Outlookiem nie powiodła się.", false);
                    logger.LogError(ex, "Outlook reconcile {JobId} failed after {ElapsedMs} ms.", job.Id, stopwatch.ElapsedMilliseconds);
                }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
    }
}
