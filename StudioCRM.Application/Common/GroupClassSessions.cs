using System.Text.Json;

namespace StudioCRM.Application.Common;

// Outlook category "Zajęcia grupowe" marks a group class: the session is created/kept as type Group, so CRM edits,
// bookings and settlement treat it as a group class (group packages, group access per location).
public static class GroupClassSessions
{
    public const string SessionType = "Group";
    public const string OutlookCategory = "Zajęcia grupowe";
    public const string DefaultTitle = "Zajęcia grupowe";

    public static bool IsGroup(string? sessionType) =>
        string.Equals(sessionType, SessionType, StringComparison.OrdinalIgnoreCase);

    public static bool HasOutlookCategory(string? categoriesJson)
    {
        try
        {
            var categories = JsonSerializer.Deserialize<List<string>>(categoriesJson ?? "[]") ?? new();
            return categories.Any(IsOutlookCategory);
        }
        catch (JsonException)
        {
            return false;
        }
    }

    // Case-insensitive; also accepts the name typed without Polish characters ("Zajecia grupowe").
    public static bool IsOutlookCategory(string? category)
    {
        var value = category?.Trim();
        return string.Equals(value, OutlookCategory, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(value, "Zajecia grupowe", StringComparison.OrdinalIgnoreCase);
    }
}
