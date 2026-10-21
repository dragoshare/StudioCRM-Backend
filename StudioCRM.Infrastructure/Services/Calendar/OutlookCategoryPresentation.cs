using System.Text.Json;
using StudioCRM.Application.DTOs.Calendar;
using StudioCRM.Domain.Entities;

namespace StudioCRM.Infrastructure.Services.Calendar;

// Category names and colours of a session as shown in schedules (same rules as the owner SessionDto):
// the trainer's own category uses the trainer colour from CRM, other categories the colour stored with the session.
internal static class OutlookCategoryPresentation
{
    internal sealed record Result(List<string> Categories, List<OutlookCategoryDto> Colors, string? Primary, string? PrimaryColor);

    internal static Result Read(Session session)
    {
        var categories = ReadList<string>(session.OutlookCategoriesJson)
            .Where(c => !string.IsNullOrWhiteSpace(c)).Select(c => c.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var colors = ReadList<OutlookCategoryDto>(session.OutlookCategoryColorsJson)
            .Where(c => !string.IsNullOrWhiteSpace(c.Name))
            .Select(c => new OutlookCategoryDto
            {
                Name = c.Name.Trim(),
                // The trainer's own category always uses the trainer colour from CRM (Outlook's may differ or be missing).
                Color = (string.Equals(c.Name.Trim(), session.Trainer?.OutlookCategoryName, StringComparison.OrdinalIgnoreCase)
                    ? Clean(session.Trainer?.OutlookCategoryColor)
                    : null) ?? Clean(c.Color)
            })
            .GroupBy(c => c.Name, StringComparer.OrdinalIgnoreCase).Select(g => g.First()).ToList();

        foreach (var category in categories.Where(c => !colors.Any(x => string.Equals(x.Name, c, StringComparison.OrdinalIgnoreCase))))
        {
            colors.Add(new OutlookCategoryDto
            {
                Name = category,
                Color = string.Equals(category, session.Trainer?.OutlookCategoryName, StringComparison.OrdinalIgnoreCase)
                    ? Clean(session.Trainer?.OutlookCategoryColor)
                    : null
            });
        }

        var primary = session.PrimaryOutlookCategory ?? categories.FirstOrDefault();
        var primaryColor = colors.FirstOrDefault(c => string.Equals(c.Name, primary, StringComparison.OrdinalIgnoreCase))?.Color;
        return new Result(categories, colors, primary, primaryColor);
    }

    private static List<T> ReadList<T>(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return new List<T>();
        try { return JsonSerializer.Deserialize<List<T>>(json) ?? new List<T>(); }
        catch (JsonException) { return new List<T>(); }
    }

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
