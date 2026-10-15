using System.Globalization;

namespace StudioCRM.Infrastructure.Services.Calendar;

// Temporary migration guard: Outlook events before the CRM start date were entered manually,
// so they must not become sessions. Remove Outlook:ImportSessionsFrom to return to the plain reconcile window.
internal static class OutlookImportCutoff
{
    private static readonly TimeZoneInfo StudioTimeZone = ResolveStudioTimeZone();

    public static bool IsBefore(DateTime eventStartUtc, string? configuredDate)
    {
        if (string.IsNullOrWhiteSpace(configuredDate))
            return false;
        if (!DateOnly.TryParseExact(configuredDate.Trim(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
            throw new InvalidOperationException("Outlook:ImportSessionsFrom must use the yyyy-MM-dd format.");

        var cutoffUtc = TimeZoneInfo.ConvertTimeToUtc(date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified), StudioTimeZone);
        var startUtc = eventStartUtc.Kind == DateTimeKind.Local ? eventStartUtc.ToUniversalTime() : eventStartUtc;
        return startUtc < cutoffUtc;
    }

    private static TimeZoneInfo ResolveStudioTimeZone()
    {
        foreach (var id in new[] { "Europe/Warsaw", "Central European Standard Time" })
        {
            try { return TimeZoneInfo.FindSystemTimeZoneById(id); }
            catch (TimeZoneNotFoundException) { }
            catch (InvalidTimeZoneException) { }
        }
        throw new InvalidOperationException("Studio time zone is not available.");
    }
}
