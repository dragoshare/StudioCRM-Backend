namespace StudioCRM.Infrastructure.Services.Calendar;

// Microsoft redirects to the callback with error/error_description instead of code when sign-in or consent fails.
// The values come from the query string (anyone can call the callback), so they are only classified and trimmed,
// never trusted as proof of anything.
public static class OutlookOAuthError
{
    public const int MaxDetailLength = 300;

    public record Result(string Code, string Message, string? MicrosoftError, string? MicrosoftCode, string? Detail);

    public static Result Describe(string? error, string? errorDescription)
    {
        var microsoftError = Clean(error, 64);
        var detail = Clean(errorDescription, MaxDetailLength);
        var aadsts = ExtractAadsts(errorDescription);

        var (code, message) = (microsoftError, aadsts) switch
        {
            (_, "AADSTS65001" or "AADSTS90094" or "AADSTS90008") or ("consent_required", _) => ("AdminConsentRequired",
                "Konto Microsoft należy do organizacji, w której zgodę na dostęp aplikacji ATLAS musi zatwierdzić administrator Microsoft 365. " +
                "Poproś administratora o zgodę albo połącz inne konto."),
            (_, "AADSTS50020" or "AADSTS50194" or "AADSTS700016" or "AADSTS50011") => ("AccountNotSupported",
                "Tego konta Microsoft nie można użyć z aplikacją ATLAS. Zaloguj się kontem, na którym prowadzisz kalendarz, " +
                "albo zgłoś problem administratorowi ATLAS."),
            ("access_denied", _) => ("AccessDenied",
                "Logowanie do Microsoft zostało anulowane albo nie udzielono zgody na dostęp do kalendarza. Spróbuj ponownie i zaakceptuj uprawnienia."),
            ("login_required" or "interaction_required", _) => ("SignInRequired",
                "Microsoft wymaga ponownego zalogowania. Spróbuj połączyć Outlook jeszcze raz."),
            ("temporarily_unavailable" or "server_error", _) => ("MicrosoftUnavailable",
                "Usługa logowania Microsoft jest chwilowo niedostępna. Spróbuj ponownie za kilka minut."),
            _ => ("MicrosoftError",
                "Microsoft odrzucił połączenie z Outlookiem. Spróbuj ponownie; jeśli błąd się powtórzy, przekaż administratorowi kod błędu.")
        };
        return new Result(code, message, microsoftError, aadsts, detail);
    }

    private static string? ExtractAadsts(string? description)
    {
        if (string.IsNullOrEmpty(description)) return null;
        var start = description.IndexOf("AADSTS", StringComparison.Ordinal);
        if (start < 0) return null;
        var end = start + 6;
        while (end < description.Length && end - start < 14 && char.IsAsciiDigit(description[end])) end++;
        return end > start + 6 ? description[start..end] : null;
    }

    // Keeps the first line, drops control characters and limits the length.
    private static string? Clean(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var firstLine = value.Split('\n', '\r')[0];
        var cleaned = new string(firstLine.Where(c => !char.IsControl(c)).ToArray()).Trim();
        if (cleaned.Length == 0) return null;
        return cleaned.Length <= maxLength ? cleaned : cleaned[..maxLength];
    }
}
