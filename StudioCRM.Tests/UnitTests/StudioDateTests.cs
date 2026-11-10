using StudioCRM.Application.Common;

namespace StudioCRM.Tests.UnitTests;

public class StudioDateTests
{
    [Theory]
    [InlineData("2026-11-04T22:30:00Z", "2026-11-04")] // 23:30 in Warsaw (CET)
    [InlineData("2026-11-04T23:30:00Z", "2026-11-05")] // already 00:30 the next day in Warsaw
    [InlineData("2026-07-01T22:15:00Z", "2026-07-02")] // CEST: 00:15 the next day
    public void TodayAsStoredDateUsesStudioCalendarDayAtUtcMidnight(string utcNow, string expectedDate)
    {
        var now = DateTime.Parse(utcNow, null, System.Globalization.DateTimeStyles.AdjustToUniversal | System.Globalization.DateTimeStyles.AssumeUniversal);
        var stored = StudioMonth.TodayAsStoredDate(now);
        Assert.Equal(DateTime.SpecifyKind(DateTime.Parse(expectedDate), DateTimeKind.Utc), stored);
        Assert.Equal(DateTimeKind.Utc, stored.Kind);
    }
}
