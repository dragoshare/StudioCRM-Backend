using StudioCRM.Application.DTOs.Sessions;
using StudioCRM.Domain.Entities;

namespace StudioCRM.Infrastructure.Services;

internal static class GroupBookingPolicy
{
    public static int ValidateMinutes(int value)
    {
        if (value < 0 || value > 525600)
            throw new InvalidOperationException("Booking deadline must be between 0 and 525600 minutes.");
        return value;
    }

    public static bool IsGroup(Session session) => session.IsPubliclyBookable ||
        string.Equals(session.ActualSessionType ?? session.PlannedSessionType, "Group", StringComparison.OrdinalIgnoreCase);

    public static int BookedSeats(Session session) => session.Participants.Count(p =>
        p.AttendanceStatus != "CancelledInTime" && p.AttendanceStatus != "CancelledLate");

    public static int? AvailableSeats(Session session) => session.PublicCapacity.HasValue
        ? Math.Max(0, session.PublicCapacity.Value - BookedSeats(session)) : null;

    public static bool IsFullyBooked(Session session) => session.PublicCapacity.HasValue &&
        BookedSeats(session) >= session.PublicCapacity.Value;

    public static BookingRulesDto Describe(Session session) => new()
    {
        RegistrationClosesBeforeMinutes = session.RegistrationClosesBeforeMinutes,
        CancellationClosesBeforeMinutes = session.CancellationClosesBeforeMinutes,
        RegistrationClosesAtUtc = session.StartAt.AddMinutes(-session.RegistrationClosesBeforeMinutes),
        CancellationClosesAtUtc = session.StartAt.AddMinutes(-session.CancellationClosesBeforeMinutes)
    };

    public static void EnsureRegistrationOpen(Session session, DateTime now)
    {
        if (now >= Describe(session).RegistrationClosesAtUtc)
            throw new InvalidOperationException("Registration deadline has passed.");
    }

    public static void EnsureCancellationOpen(Session session, DateTime now)
    {
        if (now >= Describe(session).CancellationClosesAtUtc)
            throw new InvalidOperationException("Cancellation deadline has passed. Contact the studio.");
    }
}
