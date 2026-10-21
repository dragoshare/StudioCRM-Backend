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
        Assert.Equal("Magdalena M [KŁAJ] - Konsultacja", SessionTitleBuilder.BuildOutlookSubject(session));
    }

    // Outlook shows "Title - Konsultacja" (or just "Konsultacja"); the suffix is never copied back nor doubled.
    [Theory]
    [InlineData("Anna T", "Anna T", "Anna T - Konsultacja")]
    [InlineData("Anna T - Konsultacja", "Anna T", "Anna T - Konsultacja")]
    [InlineData("Anna T - konsultacja - Konsultacja", "Anna T", "Anna T - Konsultacja")]
    [InlineData("Konsultacja", "Konsultacja", "Konsultacja")]
    [InlineData("", "Konsultacja", "Konsultacja")]
    [InlineData(null, "Konsultacja", "Konsultacja")]
    public void OutlookSubjectSuffixMarksConsultation(string? subject, string crmTitle, string outlookSubject)
    {
        Assert.Equal(crmTitle, ConsultationSessions.StripOutlookSubjectSuffix(subject));
        Assert.Equal(outlookSubject, ConsultationSessions.BuildOutlookSubject(subject));
    }
}
