using StudioCRM.Infrastructure.Services.Calendar;

namespace StudioCRM.Tests.UnitTests;

public class OutlookImportCutoffTests
{
    [Theory]
    // 30 September 23:30 in Poland is still before the 1 October studio day.
    [InlineData("2026-09-30T21:30:00Z", true)]
    // Midnight 1 October in Poland is 22:00 UTC on 30 September.
    [InlineData("2026-09-30T22:00:00Z", false)]
    [InlineData("2026-10-15T15:00:00Z", false)]
    public void SkipsEventsBeforeStudioStartDate(string startUtc, bool skipped)
    {
        var start = DateTime.Parse(startUtc, null, System.Globalization.DateTimeStyles.AdjustToUniversal);
        Assert.Equal(skipped, OutlookImportCutoff.IsBefore(start, "2026-10-01"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void NoCutoffKeepsDefaultImportWindow(string? configured)
    {
        Assert.False(OutlookImportCutoff.IsBefore(new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc), configured));
    }

    [Fact]
    public void InvalidCutoffFailsLoudly()
    {
        Assert.Throws<InvalidOperationException>(() => OutlookImportCutoff.IsBefore(DateTime.UtcNow, "01.10.2026"));
    }
}
