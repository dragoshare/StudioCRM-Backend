using StudioCRM.Infrastructure.Services.Calendar;

namespace StudioCRM.Tests.UnitTests;

public class OutlookImportDiagnosisTests
{
    private static OutlookImportDiagnosis.Facts Facts(
        string? type = "singleInstance", bool knownLocation = true, bool createdByCrm = false, bool beforeCutoff = false,
        bool past = false, bool imported = true, bool converted = false, int? sessionId = null, bool sessionExists = false,
        string? status = null, bool linkedElsewhere = false, params string[] warnings) =>
        new(type, knownLocation, createdByCrm, beforeCutoff, past, imported, converted, sessionId, sessionExists, status,
            linkedElsewhere, warnings);

    private static string Code(OutlookImportDiagnosis.Facts facts) => OutlookImportDiagnosis.Decide(facts).Code;

    [Fact]
    public void FollowsTheWebhookOrderOfChecks()
    {
        Assert.Equal("SeriesMaster", Code(Facts(type: "seriesMaster", knownLocation: false)));
        // An unknown location is dropped before CRM stores anything.
        Assert.Equal("UnknownLocation", Code(Facts(knownLocation: false, imported: false)));
        Assert.Equal("NotReceived", Code(Facts(imported: false, createdByCrm: true)));
        Assert.Equal("Linked", Code(Facts(sessionId: 5, sessionExists: true, status: "Planned", createdByCrm: true)));
        Assert.Equal("LinkedCancelled", Code(Facts(sessionId: 5, sessionExists: true, status: "Cancelled")));
        Assert.Equal("SessionMissing", Code(Facts(sessionId: 5)));
        Assert.Equal("BeforeImportCutoff", Code(Facts(beforeCutoff: true, past: true)));
        Assert.Equal("PastNeedsReview", Code(Facts(past: true, warnings: ["x"])));
        Assert.Equal("NotImported", Code(Facts()));
    }

    [Fact]
    public void LeftoverCopyInAnotherCalendarCanBeDeletedWithoutTouchingTheSession()
    {
        var (code, explanation) = OutlookImportDiagnosis.Decide(
            Facts(sessionId: 586, sessionExists: true, status: "Planned", linkedElsewhere: true));
        Assert.Equal("StaleCopy", code);
        Assert.Contains("#586", explanation);
        Assert.Contains("nie zmienia sesji", explanation);
    }

    [Fact]
    public void CrmCreatedEventWithoutSessionIsReportedEvenWhenTheConvertedFlagRemains()
    {
        // The import resets the flag of a deleted session but still skips events CRM created itself.
        Assert.Equal("CrmEventWithoutSession", Code(Facts(createdByCrm: true, converted: true)));
        Assert.Equal("SessionDeleted", Code(Facts(converted: true)));
    }

    [Fact]
    public void MappingWarningsAreTheExplanation()
    {
        var (code, explanation) = OutlookImportDiagnosis.Decide(Facts(warnings: ["Nie znaleziono klienta: a@b.pl", "Drugie."]));
        Assert.Equal("MappingWarning", code);
        Assert.Equal("Nie znaleziono klienta: a@b.pl Drugie.", explanation);
    }
}
