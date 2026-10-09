using StudioCRM.Domain.Entities;
using StudioCRM.Infrastructure.Services;

namespace StudioCRM.Tests.UnitTests;

public class GroupBookingPolicyTests
{
    private static Session Class() => new()
    {
        StartAt = new DateTime(2026, 10, 15, 14, 0, 0, DateTimeKind.Utc),
        RegistrationClosesBeforeMinutes = 30,
        CancellationClosesBeforeMinutes = 720,
        PlannedSessionType = "Group",
        PublicCapacity = 2
    };

    [Fact]
    public void RegistrationClosesExactlyAtDeadline()
    {
        var session = Class();
        var deadline = session.StartAt.AddMinutes(-30);
        GroupBookingPolicy.EnsureRegistrationOpen(session, deadline.AddTicks(-1));
        Assert.Throws<InvalidOperationException>(() => GroupBookingPolicy.EnsureRegistrationOpen(session, deadline));
        Assert.Throws<InvalidOperationException>(() => GroupBookingPolicy.EnsureRegistrationOpen(session, session.StartAt));
    }

    [Fact]
    public void CancellationClosesExactlyAtDeadline()
    {
        var session = Class();
        var deadline = session.StartAt.AddHours(-12);
        GroupBookingPolicy.EnsureCancellationOpen(session, deadline.AddTicks(-1));
        Assert.Throws<InvalidOperationException>(() => GroupBookingPolicy.EnsureCancellationOpen(session, deadline));
        Assert.Throws<InvalidOperationException>(() => GroupBookingPolicy.EnsureCancellationOpen(session, session.StartAt.AddDays(1)));
    }

    [Fact]
    public void LegacyZeroDeadlineClosesAtStart()
    {
        var session = Class();
        session.RegistrationClosesBeforeMinutes = session.CancellationClosesBeforeMinutes = 0;
        GroupBookingPolicy.EnsureRegistrationOpen(session, session.StartAt.AddSeconds(-1));
        GroupBookingPolicy.EnsureCancellationOpen(session, session.StartAt.AddSeconds(-1));
        Assert.Throws<InvalidOperationException>(() => GroupBookingPolicy.EnsureCancellationOpen(session, session.StartAt));
    }

    [Fact]
    public void OccupancyExcludesBothCancellationStatuses()
    {
        var session = Class();
        foreach (var status in new[] { "Planned", "Present", "CancelledInTime", "CancelledLate" })
            session.Participants.Add(new SessionParticipant { AttendanceStatus = status });
        Assert.Equal(2, GroupBookingPolicy.BookedSeats(session));
        Assert.Equal(0, GroupBookingPolicy.AvailableSeats(session));
        Assert.True(GroupBookingPolicy.IsFullyBooked(session));
        session.PublicCapacity = null;
        Assert.Null(GroupBookingPolicy.AvailableSeats(session));
        Assert.False(GroupBookingPolicy.IsFullyBooked(session));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(525601)]
    public void InvalidDeadlineRejected(int minutes) =>
        Assert.Throws<InvalidOperationException>(() => GroupBookingPolicy.ValidateMinutes(minutes));

    [Fact]
    public void GroupClassificationDoesNotDependOnHeadcount()
    {
        var session = Class();
        Assert.True(GroupBookingPolicy.IsGroup(session));
        session.PlannedSessionType = "SemiPersonal4";
        Assert.False(GroupBookingPolicy.IsGroup(session));
        session.IsPubliclyBookable = true;
        Assert.True(GroupBookingPolicy.IsGroup(session));
    }
}
