using StudioCRM.Application.Common;
using StudioCRM.Domain.Entities;
using StudioCRM.Domain.Enums;

namespace StudioCRM.Tests.UnitTests;

public class SessionTitleBuilderTests
{
    [Theory]
    [InlineData(true, null)]
    [InlineData(false, "Group")]
    [InlineData(false, "group")]
    public void GroupClassTitleIsNotDerivedFromParticipants(
        bool isPubliclyBookable,
        string? plannedSessionType)
    {
        Assert.False(SessionTitleBuilder.ShouldDeriveFromParticipants(
            isPubliclyBookable,
            plannedSessionType));
    }

    [Fact]
    public void PublicGroupClassOutlookSubjectKeepsClassName()
    {
        var session = BuildSession(isPubliclyBookable: true, SessionBillingType.Group.ToString());

        var subject = SessionTitleBuilder.BuildOutlookSubject(session);

        Assert.Equal("StudioCRM: Mobility", subject);
    }

    [Fact]
    public void SemiPersonalOutlookSubjectStillIncludesParticipants()
    {
        var session = BuildSession(isPubliclyBookable: false, SessionBillingType.TwoToOne.ToString());

        var subject = SessionTitleBuilder.BuildOutlookSubject(session);

        Assert.Equal("StudioCRM: Aldona W + Agata M - Aldona Wrona + Agata Mazur", subject);
    }

    private static Session BuildSession(bool isPubliclyBookable, string? plannedSessionType)
    {
        var aldona = new Client { FirstName = "Aldona", LastName = "Wrona" };
        var agata = new Client { FirstName = "Agata", LastName = "Mazur" };

        return new Session
        {
            Title = isPubliclyBookable ? "Mobility" : "Aldona W + Agata M",
            IsPubliclyBookable = isPubliclyBookable,
            PlannedSessionType = plannedSessionType,
            Participants = new List<SessionParticipant>
            {
                new() { Client = aldona },
                new() { Client = agata }
            }
        };
    }
}
