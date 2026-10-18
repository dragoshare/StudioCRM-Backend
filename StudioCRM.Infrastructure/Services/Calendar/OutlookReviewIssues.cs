using System.Text.Json;
using StudioCRM.Domain.Entities;

namespace StudioCRM.Infrastructure.Services.Calendar;

// Outlook changes that must not touch CRM settlement automatically. They are stored as mapping warnings on the
// Outlook event (the existing issues list) with a common prefix, so the owner sees them for every trainer's
// calendar and decides in CRM (session correction, cancellation or "convert to session").
public static class OutlookReviewIssues
{
    public const string Prefix = "Do weryfikacji w CRM: ";

    public const string CompletedSessionDeletedType = "CompletedSessionDeletedInOutlook";
    public const string StartedSessionDeletedType = "StartedSessionDeletedInOutlook";
    public const string CompletedSessionAttendeeRemovedType = "CompletedSessionAttendeeRemovedInOutlook";
    public const string PastEventNeedsReviewType = "PastEventNeedsReview";

    public static string CompletedSessionDeleted() =>
        Prefix + "zrealizowaną sesję usunięto w Outlooku. Rozliczenie zostało bez zmian; jeśli sesja się nie odbyła, skoryguj ją w CRM.";

    public static string StartedSessionDeleted() =>
        Prefix + "rozpoczętą lub zakończoną sesję usunięto w Outlooku. Nie anulowano jej automatycznie; jeśli się nie odbyła, anuluj ją w CRM.";

    public static string CompletedSessionAttendeeRemoved(string email) =>
        Prefix + "klienta usunięto w Outlooku z zrealizowanej sesji: " + email + ". Wejście nadal jest rozliczone; jeśli klient nie był obecny, skoryguj sesję w CRM.";

    public static string PastEventNeedsReview() =>
        Prefix + "wydarzenie z przeszłości nie zostało automatycznie zamienione na sesję (rozliczyłoby wejścia bez potwierdzenia). Jeśli trening się odbył, użyj „Zamień na sesję”.";

    public static bool IsReview(string warning) => warning.StartsWith(Prefix, StringComparison.Ordinal);

    public static string? ResolveType(string warning)
    {
        if (!IsReview(warning)) return null;
        if (warning.Contains("zrealizowaną sesję usunięto", StringComparison.Ordinal)) return CompletedSessionDeletedType;
        if (warning.Contains("rozpoczętą lub zakończoną sesję", StringComparison.Ordinal)) return StartedSessionDeletedType;
        if (warning.Contains("z zrealizowanej sesji", StringComparison.Ordinal)) return CompletedSessionAttendeeRemovedType;
        if (warning.Contains("wydarzenie z przeszłości", StringComparison.Ordinal)) return PastEventNeedsReviewType;
        return "ReviewInCrm";
    }

    // Adds the warning once (repeated notifications or reconciles must not duplicate it).
    public static bool Add(ExternalCalendarEvent evt, string warning)
    {
        var warnings = Read(evt.MappingWarningsJson);
        if (warnings.Contains(warning)) return false;
        warnings.Add(warning);
        evt.MappingWarningsJson = JsonSerializer.Serialize(warnings);
        return true;
    }

    public static void RemovePastEventReview(ExternalCalendarEvent evt)
    {
        var warnings = Read(evt.MappingWarningsJson);
        if (warnings.RemoveAll(w => w == PastEventNeedsReview()) > 0)
            evt.MappingWarningsJson = JsonSerializer.Serialize(warnings);
    }

    private static List<string> Read(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return new List<string>();
        try { return JsonSerializer.Deserialize<List<string>>(json) ?? new List<string>(); }
        catch (JsonException) { return new List<string>(); }
    }
}
