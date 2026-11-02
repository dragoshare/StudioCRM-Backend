using StudioCRM.Infrastructure.Services.Calendar;

namespace StudioCRM.Tests.UnitTests;

public class OutlookEventCopyTests
{
    [Fact]
    public void EventIsALeftoverOnlyWhenTheSessionIsLinkedToAnotherEvent()
    {
        // #1051: the session moved to Karolina's calendar (2), Bartosz's event (1) stayed.
        var moved = new[] { (2, "karolina-evt") };
        Assert.True(OutlookEventCopy.IsLeftover(moved, 1, "bartosz-evt"));
        Assert.False(OutlookEventCopy.IsLeftover(moved, 2, "karolina-evt"));
        // Same calendar, recreated event: the old id is a leftover too.
        Assert.True(OutlookEventCopy.IsLeftover(moved, 2, "older-karolina-evt"));
        // Event ids are compared exactly, as Graph returns them.
        Assert.True(OutlookEventCopy.IsLeftover(moved, 2, "KAROLINA-EVT"));
        // No link (e.g. a hand-over row or a link already removed): the row still speaks for the session.
        Assert.False(OutlookEventCopy.IsLeftover(Array.Empty<(int, string)>(), 1, "bartosz-evt"));
    }
}
