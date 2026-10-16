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
}
