namespace StudioCRM.Infrastructure.Services.Calendar;

// Outlook events created by CRM carry this Graph transactionId. The webhook can arrive before the CRM
// transaction that created the event commits (e.g. a session saved directly as completed), so the importer
// must recognise its own events instead of mapping them to a second session; CRM links them itself.
public static class CrmOutlookEventMarker
{
    private const string Prefix = "studiocrm-session-";

    // Unique per create call: Graph treats a repeated transactionId as the same create request.
    public static string Create(int sessionId) => $"{Prefix}{sessionId}-{Guid.NewGuid():N}";

    public static bool IsCrmCreated(string? transactionId) =>
        transactionId is not null && transactionId.StartsWith(Prefix, StringComparison.Ordinal);

    // CRM marks only single events; its own series are created without a transactionId. An occurrence that carries
    // the marker belongs to a series the trainer made in Outlook out of a CRM event (Anna K, 5.11.2026), so it is
    // the trainer's own event and must be imported.
    public static bool IsCrmCreated(string? transactionId, string? eventType) =>
        IsCrmCreated(transactionId) &&
        (eventType is null || string.Equals(eventType, "singleInstance", StringComparison.OrdinalIgnoreCase));
}
