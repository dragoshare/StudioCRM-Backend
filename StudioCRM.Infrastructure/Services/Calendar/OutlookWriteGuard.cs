using System.Collections.Concurrent;

namespace StudioCRM.Infrastructure.Services.Calendar;

// Every create/update of an Outlook event with attendees makes Exchange e-mail each attendee from the trainer's
// mailbox. A sync loop (webhook <-> CRM, a retried job) therefore turns into mass mailing and Microsoft 365 blocks
// the mailbox. This guard caps writes per event, whatever code path issues them; a blocked write is only logged.
// The limits separate a loop (21.10.2026: a write every ~2 s) from a person rearranging the schedule: at most
// MaxWritesPerMinute writes within a minute and MaxWritesPerHour within an hour per event.
public static class OutlookWriteGuard
{
    public const int MaxWritesPerMinute = 3;
    public const int MaxWritesPerHour = 30;
    private static readonly TimeSpan Minute = TimeSpan.FromMinutes(1);
    private static readonly TimeSpan Hour = TimeSpan.FromHours(1);
    private static readonly ConcurrentDictionary<string, Queue<DateTime>> Writes = new(StringComparer.OrdinalIgnoreCase);

    public static bool TryAcquire(string key) => TryAcquire(key, DateTime.UtcNow);

    public static bool TryAcquire(string key, DateTime now)
    {
        var writes = Writes.GetOrAdd(key, _ => new Queue<DateTime>());
        lock (writes)
        {
            while (writes.Count > 0 && now - writes.Peek() >= Hour)
                writes.Dequeue();
            if (writes.Count >= MaxWritesPerHour || writes.Count(w => now - w < Minute) >= MaxWritesPerMinute)
                return false;
            writes.Enqueue(now);
        }

        if (Writes.Count > 10_000)
            foreach (var (staleKey, stale) in Writes)
                lock (stale)
                    if (stale.Count == 0 || now - stale.Last() >= Hour)
                        Writes.TryRemove(staleKey, out _);
        return true;
    }

    public static string SessionKey(Guid organizationId, int sessionId) => $"session:{organizationId}:{sessionId}";

    public static string EventKey(int calendarIntegrationId, string externalEventId) => $"event:{calendarIntegrationId}:{externalEventId}";
}
