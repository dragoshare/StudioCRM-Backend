using StudioCRM.Application.Common;
using StudioCRM.Domain.Entities;

namespace StudioCRM.Tests.UnitTests;

public class ConsultationSessionTests
{
    [Theory]
    [InlineData("[\"Aleksandra Szcząchor\",\"Konsultacje\"]", true)]
    [InlineData("[\"konsultacje \"]", true)]
    [InlineData("[\"Aleksandra Szcząchor\"]", false)]
    [InlineData("not json", false)]
    [InlineData(null, false)]
    public void ConsultationIsRecognisedByOutlookCategory(string? categoriesJson, bool expected)
        => Assert.Equal(expected, ConsultationSessions.HasOutlookCategory(categoriesJson));

    [Fact]
    public void ConsultationKeepsItsOwnTitleInsteadOfClientNames()
    {
        Assert.False(SessionTitleBuilder.ShouldDeriveFromParticipants(false, ConsultationSessions.SessionType));
        var session = new Session { Title = "Magdalena M [KŁAJ]", PlannedSessionType = ConsultationSessions.SessionType };
        Assert.Equal("Magdalena M [KŁAJ]", SessionTitleBuilder.BuildOutlookSubject(session));
    }
}
