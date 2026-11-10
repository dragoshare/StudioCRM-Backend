using StudioCRM.Application.Common;
using StudioCRM.Domain.Entities;

namespace StudioCRM.Tests.UnitTests;

// A planned personal session follows its attendee list, because the manual completion starts from the planned type.
public class PersonalSessionTypeTests
{
    [Theory]
    [InlineData("OneToOne", 2, "TwoToOne")]
    [InlineData("TwoToOne", 1, "OneToOne")]
    [InlineData("TwoToOne", 4, "FourToOne")]
    [InlineData("OneToOne", 0, "OneToOne")]
    [InlineData("OneToOne", 5, "OneToOne")]
    public void PlannedPersonalSessionFollowsParticipants(string planned, int participants, string expected)
    {
        var session = new Session { PlannedSessionType = planned };
        PersonalSessionType.FollowParticipants(session, participants);
        Assert.Equal(expected, session.PlannedSessionType);
    }

    [Theory]
    [InlineData("Group", false, "Planned")]
    [InlineData("Consultation", false, "Planned")]
    [InlineData("OneToOne", true, "Planned")]
    [InlineData("OneToOne", false, "Completed")]
    public void GroupConsultationAndFinishedSessionsKeepTheirType(string planned, bool isPublic, string status)
    {
        var session = new Session { PlannedSessionType = planned, IsPubliclyBookable = isPublic, Status = status };
        PersonalSessionType.FollowParticipants(session, 2);
        Assert.Equal(planned, session.PlannedSessionType);
    }
}
