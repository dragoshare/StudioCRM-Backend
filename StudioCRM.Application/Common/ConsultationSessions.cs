using System.Text.Json;

namespace StudioCRM.Application.Common;

// A consultation is trainer work time without clients, packages or balance (often before the client exists in CRM).
// It is paid at the trainer's hourly rate like any other completed session.
public static class ConsultationSessions
{
    public const string SessionType = "Consultation";
    public const string OutlookCategory = "Konsultacje";
    public const string DefaultTitle = "Konsultacja";
    // Outlook category colour (green) and subject suffix that mark a consultation in the trainer's calendar.
    public const string OutlookCategoryColor = "preset4";
    public const string OutlookSubjectSuffix = " - " + DefaultTitle;

    // "Anna T - Konsultacja"; without a title just "Konsultacja".
    public static string BuildOutlookSubject(string? title)
    {
        var value = StripOutlookSubjectSuffix(title);
        return string.Equals(value, DefaultTitle, StringComparison.OrdinalIgnoreCase) ? DefaultTitle : value + OutlookSubjectSuffix;
    }

    // Title for CRM from an Outlook subject, without the suffix CRM itself added.
    public static string StripOutlookSubjectSuffix(string? subject)
    {
        var value = subject?.Trim() ?? string.Empty;
        while (value.EndsWith(OutlookSubjectSuffix.Trim(), StringComparison.OrdinalIgnoreCase) &&
               value.Length > DefaultTitle.Length)
        {
            value = value[..^DefaultTitle.Length].TrimEnd();
            if (value.EndsWith('-')) value = value[..^1].TrimEnd();
        }
        return value.Length == 0 ? DefaultTitle : value;
    }

    public static bool IsConsultation(string? sessionType) =>
        string.Equals(sessionType, SessionType, StringComparison.OrdinalIgnoreCase);

    public static bool HasOutlookCategory(string? categoriesJson)
    {
        try
        {
            var categories = JsonSerializer.Deserialize<List<string>>(categoriesJson ?? "[]") ?? new();
            return categories.Any(c => string.Equals(c?.Trim(), OutlookCategory, StringComparison.OrdinalIgnoreCase));
        }
        catch (JsonException)
        {
            return false;
        }
    }
}
