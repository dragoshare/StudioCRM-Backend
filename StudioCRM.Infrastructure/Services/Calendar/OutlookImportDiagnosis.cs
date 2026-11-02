namespace StudioCRM.Infrastructure.Services.Calendar;

// Explains, in the order OutlookWebhookService.ImportOrUpdateEventAsync applies its checks, why an Outlook event
// is or is not a CRM session. Keep it in step with that method.
internal static class OutlookImportDiagnosis
{
    internal sealed record Facts(
        string? Type,
        bool KnownLocation,
        bool CreatedByCrm,
        bool BeforeImportCutoff,
        bool Past,
        bool Imported,
        bool IsConvertedToSession,
        int? SessionId,
        bool SessionExists,
        string? SessionStatus,
        bool LinkedElsewhere,
        IReadOnlyList<string> Warnings);

    internal static (string Code, string Explanation) Decide(Facts f)
    {
        if (string.Equals(f.Type, "seriesMaster", StringComparison.OrdinalIgnoreCase))
            return ("SeriesMaster", "Główne wydarzenie serii; CRM importuje jej pojedyncze wystąpienia.");
        if (!f.KnownLocation)
            return ("UnknownLocation", "Lokalizacja nie pasuje do żadnej aktywnej lokalizacji CRM (ani nazwą, ani adresem sali). " +
                "Takie wydarzenie jest pomijane bez ostrzeżenia.");
        if (!f.Imported)
            return ("NotReceived", "CRM nie ma tego wydarzenia: nie dotarło powiadomienie z Outlooka albo jego obsługa się nie powiodła. " +
                "Uzgodnienie kalendarza (POST /api/outlook/reconcile) zaimportuje je ponownie.");
        if (f.SessionId.HasValue)
        {
            if (f.LinkedElsewhere)
                return ("StaleCopy", $"Wydarzenie wskazuje sesję #{f.SessionId}, ale ta sesja jest połączona z wydarzeniem w innym kalendarzu. " +
                    "To pozostałość po przeniesieniu: usunięcie jej w Outlooku anuluje zaplanowaną sesję, a zmiana terminu ją przesunie.");
            if (!f.SessionExists)
                return ("SessionMissing", $"Wydarzenie wskazuje sesję #{f.SessionId}, której nie ma w CRM.");
            if (string.Equals(f.SessionStatus, "Cancelled", StringComparison.OrdinalIgnoreCase))
                return ("LinkedCancelled", $"Połączone z anulowaną sesją #{f.SessionId}; zmiany z Outlooka jej nie przywrócą.");
            return ("Linked", $"Połączone z sesją CRM #{f.SessionId}.");
        }
        if (f.CreatedByCrm)
            return ("CrmEventWithoutSession", "Wydarzenie utworzył CRM, a jego sesji w CRM już nie ma. Import celowo pomija wydarzenia " +
                "utworzone przez CRM, więc samo nie wróci: utwórz je od nowa w Outlooku albo dodaj sesję w CRM.");
        if (f.BeforeImportCutoff)
            return ("BeforeImportCutoff", "Wydarzenie jest przed datą startu CRM (Outlook:ImportSessionsFrom) i nie jest importowane.");
        if (f.Past)
            return ("PastNeedsReview", "Wydarzenie z przeszłości nie tworzy sesji automatycznie; zamień je na sesję w Problemach Outlook.");
        if (f.Warnings.Count > 0)
            return ("MappingWarning", string.Join(" ", f.Warnings));
        if (f.IsConvertedToSession)
            return ("SessionDeleted", "Sesja tego wydarzenia została usunięta w CRM; następna zmiana wydarzenia albo uzgodnienie kalendarza zaimportuje je ponownie.");
        return ("NotImported", "CRM zapisał wydarzenie, ale nie utworzył sesji ani ostrzeżenia (np. błąd podczas mapowania). " +
            "Uzgodnienie kalendarza spróbuje ponownie.");
    }
}
