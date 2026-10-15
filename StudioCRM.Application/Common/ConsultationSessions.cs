using System.Text.Json;

namespace StudioCRM.Application.Common;

// A consultation is trainer work time without clients, packages or balance (often before the client exists in CRM).
// It is paid at the trainer's hourly rate like any other completed session.
public static class ConsultationSessions
{
    public const string SessionType = "Consultation";
    public const string OutlookCategory = "Konsultacje";
    public const string DefaultTitle = "Konsultacja";

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
