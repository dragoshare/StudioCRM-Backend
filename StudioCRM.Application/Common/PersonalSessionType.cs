using StudioCRM.Domain.Entities;
using StudioCRM.Domain.Enums;

namespace StudioCRM.Application.Common;

// The planned type of a personal session follows its attendee list, because a manual completion starts from it.
// Group classes, consultations and finished sessions keep their type.
public static class PersonalSessionType
{
    public static string? ForParticipantCount(int count) => count switch
    {
        1 => nameof(SessionBillingType.OneToOne),
        2 => nameof(SessionBillingType.TwoToOne),
        3 => nameof(SessionBillingType.ThreeToOne),
        4 => nameof(SessionBillingType.FourToOne),
        _ => null
    };

    public static void FollowParticipants(Session session, int activeParticipantCount)
    {
        if (session.Status != "Planned" ||
            !SessionTitleBuilder.ShouldDeriveFromParticipants(session.IsPubliclyBookable, session.PlannedSessionType))
            return;

        var type = ForParticipantCount(activeParticipantCount);
        if (type != null)
            session.PlannedSessionType = type;
    }
}
