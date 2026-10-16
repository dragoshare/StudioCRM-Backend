using StudioCRM.Infrastructure.Services.Calendar;

namespace StudioCRM.Tests.UnitTests;

public class CrmOutlookEventMarkerTests
{
    [Fact]
    public void MarkerIsUniquePerCreateAndRecognised()
    {
        var first = CrmOutlookEventMarker.Create(275);
        var second = CrmOutlookEventMarker.Create(275);

        Assert.NotEqual(first, second);
        Assert.True(CrmOutlookEventMarker.IsCrmCreated(first));
        Assert.StartsWith("studiocrm-session-275-", first);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("7c5b0a2e-outlook-client-id")]
    public void EventsCreatedOutsideCrmAreNotMarked(string? transactionId)
    {
        Assert.False(CrmOutlookEventMarker.IsCrmCreated(transactionId));
    }
}
