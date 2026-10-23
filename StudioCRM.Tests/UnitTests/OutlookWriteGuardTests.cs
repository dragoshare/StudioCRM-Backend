using StudioCRM.Infrastructure.Services.Calendar;

namespace StudioCRM.Tests.UnitTests;

// A loop writes every few seconds; a person rearranging the schedule edits the same event a few times an hour.
public class OutlookWriteGuardTests
{
    [Fact]
    public void LoopIsStoppedWithinAMinuteButScheduleEditsGoThrough()
    {
        var key = $"event:test:{Guid.NewGuid()}";
        var start = new DateTime(2026, 10, 23, 10, 0, 0, DateTimeKind.Utc);

        // Loop: a write every 2 seconds is cut after MaxWritesPerMinute.
        Assert.True(OutlookWriteGuard.TryAcquire(key, start));
        Assert.True(OutlookWriteGuard.TryAcquire(key, start.AddSeconds(2)));
        Assert.True(OutlookWriteGuard.TryAcquire(key, start.AddSeconds(4)));
        Assert.False(OutlookWriteGuard.TryAcquire(key, start.AddSeconds(6)));

        // A person editing every couple of minutes keeps going (well beyond the old 6 per hour).
        var other = $"event:test:{Guid.NewGuid()}";
        for (var i = 0; i < 20; i++)
            Assert.True(OutlookWriteGuard.TryAcquire(other, start.AddMinutes(2 * i)));
    }

    [Fact]
    public void HourlyCapStillApplies()
    {
        var key = $"event:test:{Guid.NewGuid()}";
        var start = new DateTime(2026, 10, 23, 10, 0, 0, DateTimeKind.Utc);
        for (var i = 0; i < OutlookWriteGuard.MaxWritesPerHour; i++)
            Assert.True(OutlookWriteGuard.TryAcquire(key, start.AddSeconds(30 * i)));
        Assert.False(OutlookWriteGuard.TryAcquire(key, start.AddMinutes(16)));
        Assert.True(OutlookWriteGuard.TryAcquire(key, start.AddHours(1).AddMinutes(1)));
    }
}
