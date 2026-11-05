using System.Globalization;

namespace StudioCRM.Application.Common;

// A calendar month ("2026-11") in the studio time zone, as a UTC range [StartUtc, EndUtc).
public static class StudioMonth
{
    private static readonly TimeZoneInfo StudioTimeZone = ResolveStudioTimeZone();

    public static bool TryParse(string? value, out DateOnly firstDay) =>
        DateOnly.TryParseExact(value?.Trim() + "-01", "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out firstDay);

    public static (DateTime StartUtc, DateTime EndUtc) Range(string month)
    {
        if (!TryParse(month, out var firstDay))
            throw new InvalidOperationException("Month must use the yyyy-MM format.");
        return (ToUtc(firstDay), ToUtc(firstDay.AddMonths(1)));
    }

    // Today's studio calendar date in the storage convention for business dates (UTC midnight), e.g. Client.TrainingStartDate.
    public static DateTime TodayAsStoredDate(DateTime utcNow) =>
        DateTime.SpecifyKind(TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utcNow, DateTimeKind.Utc), StudioTimeZone).Date, DateTimeKind.Utc);

    private static DateTime ToUtc(DateOnly day) =>
        TimeZoneInfo.ConvertTimeToUtc(day.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified), StudioTimeZone);

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
