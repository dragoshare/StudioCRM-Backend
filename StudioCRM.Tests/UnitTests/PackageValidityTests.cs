using StudioCRM.Application.Common;

namespace StudioCRM.Tests.UnitTests;

public class PackageValidityTests
{
    [Theory]
    // Imported date at UTC midnight is 02:00 in Poland; the package must cover the whole 18 October.
    [InlineData("2026-10-18T00:00:00Z", "2026-10-18T21:59:59Z")]
    // A date picked as local midnight arrives as the previous UTC evening.
    [InlineData("2026-10-17T22:00:00Z", "2026-10-18T21:59:59Z")]
    [InlineData("2026-10-30T18:57:00Z", "2026-10-30T22:59:59Z")]
    // Winter time after the DST change on 25 October.
    [InlineData("2026-11-17T00:00:00Z", "2026-11-17T22:59:59Z")]
    public void ValidUntilCoversWholeStudioDay(string input, string expected)
    {
        var result = PackageValidity.EndOfStudioDay(DateTime.Parse(input, null, System.Globalization.DateTimeStyles.AdjustToUniversal));
        Assert.Equal(DateTime.Parse(expected, null, System.Globalization.DateTimeStyles.AdjustToUniversal), result);
        Assert.Equal(DateTimeKind.Utc, result.Kind);
    }

    [Fact]
    public void EndOfDayIsIdempotent()
    {
        var once = PackageValidity.EndOfStudioDay(new DateTime(2026, 10, 18, 0, 0, 0, DateTimeKind.Utc));
        Assert.Equal(once, PackageValidity.EndOfStudioDay(once));
        Assert.Null(PackageValidity.EndOfStudioDay((DateTime?)null));
    }
}
