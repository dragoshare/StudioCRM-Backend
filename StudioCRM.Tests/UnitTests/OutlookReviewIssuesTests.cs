using StudioCRM.Domain.Entities;
using StudioCRM.Infrastructure.Services.Calendar;

namespace StudioCRM.Tests.UnitTests;

public class OutlookReviewIssuesTests
{
    [Fact]
    public void AddsEachReviewOnceAndKeepsOtherWarnings()
    {
        var evt = new ExternalCalendarEvent { MappingWarningsJson = "[\"Nie znaleziono klienta: a@b.pl\"]" };
        Assert.True(OutlookReviewIssues.Add(evt, OutlookReviewIssues.PastEventNeedsReview()));
        Assert.False(OutlookReviewIssues.Add(evt, OutlookReviewIssues.PastEventNeedsReview()));
        Assert.Contains("Nie znaleziono klienta", evt.MappingWarningsJson);

        OutlookReviewIssues.RemovePastEventReview(evt);
        Assert.DoesNotContain(OutlookReviewIssues.Prefix, evt.MappingWarningsJson);
        Assert.Contains("Nie znaleziono klienta", evt.MappingWarningsJson);
    }

    [Fact]
    public void ResolvesStableIssueTypes()
    {
        Assert.Equal(OutlookReviewIssues.CompletedSessionDeletedType, OutlookReviewIssues.ResolveType(OutlookReviewIssues.CompletedSessionDeleted()));
        Assert.Equal(OutlookReviewIssues.StartedSessionDeletedType, OutlookReviewIssues.ResolveType(OutlookReviewIssues.StartedSessionDeleted()));
        Assert.Equal(OutlookReviewIssues.CompletedSessionAttendeeRemovedType, OutlookReviewIssues.ResolveType(OutlookReviewIssues.CompletedSessionAttendeeRemoved("a@b.pl")));
        Assert.Equal(OutlookReviewIssues.PastEventNeedsReviewType, OutlookReviewIssues.ResolveType(OutlookReviewIssues.PastEventNeedsReview()));
        Assert.Null(OutlookReviewIssues.ResolveType("Nie znaleziono klienta: a@b.pl"));
    }
}
