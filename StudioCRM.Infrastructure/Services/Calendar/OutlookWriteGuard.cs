using System.Collections.Concurrent;

namespace StudioCRM.Infrastructure.Services.Calendar;

// Every create/update of an Outlook event with attendees makes Exchange e-mail each attendee from the trainer's
// mailbox. A sync loop (webhook <-> CRM, a retried job) therefore turns into mass mailing and Microsoft 365 blocks
// the mailbox. This guard caps writes per event, whatever code path issues them; a blocked write is only logged.
public static class OutlookWriteGuard
{
    public const int MaxWritesPerHour = 6;
    private static readonly TimeSpan Window = TimeSpan.FromHours(1);
    private static readonly ConcurrentDictionary<string, Queue<DateTime>> Writes = new(StringComparer.OrdinalIgnoreCase);

    public static bool TryAcquire(string key) => TryAcquire(key, DateTime.UtcNow);

    public static bool TryAcquire(string key, DateTime now)
    {
        var writes = Writes.GetOrAdd(key, _ => new Queue<DateTime>());
        lock (writes)
        {
            while (writes.Count > 0 && now - writes.Peek() >= Window)
                writes.Dequeue();
            if (writes.Count >= MaxWritesPerHour)
                return false;
            writes.Enqueue(now);
        }

        if (Writes.Count > 10_000)
            foreach (var (staleKey, stale) in Writes)
                lock (stale)
                    if (stale.Count == 0 || now - stale.Last() >= Window)
                        Writes.TryRemove(staleKey, out _);
        return true;
    }

    public static string SessionKey(Guid organizationId, int sessionId) => $"session:{organizationId}:{sessionId}";

    public static string EventKey(int calendarIntegrationId, string externalEventId) => $"event:{calendarIntegrationId}:{externalEventId}";
}
