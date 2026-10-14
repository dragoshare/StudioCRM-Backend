namespace StudioCRM.Application.Common;

public static class PackageValidity
{
    private static readonly TimeZoneInfo StudioTimeZone = ResolveStudioTimeZone();

    // An entry package is valid through the whole last studio day, so a session on that day still counts.
    public static DateTime EndOfStudioDay(DateTime value)
    {
        var utc = value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
        };
        var studioDay = TimeZoneInfo.ConvertTimeFromUtc(utc, StudioTimeZone).Date;
        var endOfDay = DateTime.SpecifyKind(studioDay.AddDays(1).AddSeconds(-1), DateTimeKind.Unspecified);
        return TimeZoneInfo.ConvertTimeToUtc(endOfDay, StudioTimeZone);
    }

    public static DateTime? EndOfStudioDay(DateTime? value) =>
        value.HasValue ? EndOfStudioDay(value.Value) : null;

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
