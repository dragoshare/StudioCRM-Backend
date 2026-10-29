using System.Net;
using System.Reflection;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using StudioCRM.Application.Interfaces.Calendar;
using StudioCRM.Domain.Entities;
using StudioCRM.Infrastructure.Persistence;
using StudioCRM.Infrastructure.Services.Calendar;

namespace StudioCRM.Tests.IntegrationTests;

// Outlook must never change CRM settlement silently: settled or started sessions are reported to the owner,
// an empty calendar is treated as a broken connection, and past events wait for an explicit conversion.
public class OutlookSettlementSafetyTests
{
    [PostgresFact]
    public async Task DeletingEventsCancelsOnlyFutureSessionsAndReportsSettledOrStartedOnes()
    {
        await using var database = await TestDatabase.CreateAsync();
        await using var db = database.Context();
        var (integration, trainer, location) = await Seed(db, "delete");
        Session Make(string status, DateTime start) => new()
        {
            Title = status, Trainer = trainer, Location = location, Status = status, StartAt = start, EndAt = start.AddHours(1)
        };
        var completed = Make("Completed", DateTime.UtcNow.AddDays(-2));
        var started = Make("Planned", DateTime.UtcNow.AddMinutes(-30));
        var future = Make("Planned", DateTime.UtcNow.AddDays(2));
        db.AddRange(completed, started, future);
        await db.SaveChangesAsync();
        foreach (var session in new[] { completed, started, future })
        {
            db.Add(new CalendarEventLink { SessionId = session.Id, CalendarIntegrationId = integration.Id, ExternalEventId = $"evt-{session.Id}" });
            db.Add(new ExternalCalendarEvent
            {
                CalendarIntegrationId = integration.Id, ExternalEventId = $"evt-{session.Id}", Subject = session.Title,
                StartAt = session.StartAt, EndAt = session.EndAt, SessionId = session.Id, IsConvertedToSession = true
            });
        }
        await db.SaveChangesAsync();
        var service = Service(db, new GraphHandler(_ => null), integration.UserId);

        foreach (var session in new[] { completed, started, future })
            await Invoke(service, "MarkDeletedAsync", integration.Id, $"evt-{session.Id}");

        db.ChangeTracker.Clear();
        Assert.Equal("Completed", (await db.Sessions.SingleAsync(s => s.Id == completed.Id)).Status);
        Assert.Equal("Planned", (await db.Sessions.SingleAsync(s => s.Id == started.Id)).Status);
        Assert.Equal("Cancelled", (await db.Sessions.SingleAsync(s => s.Id == future.Id)).Status);
        Assert.Equal([completed.Id, started.Id], (await db.CalendarEventLinks.Select(l => l.SessionId).OrderBy(id => id).ToListAsync()));
        Assert.Contains(OutlookReviewIssues.CompletedSessionDeleted(), await Warnings(db, completed.Id));
        Assert.Contains(OutlookReviewIssues.StartedSessionDeleted(), await Warnings(db, started.Id));
        Assert.Empty(await Warnings(db, future.Id));
    }

    [PostgresFact]
    public async Task EmptyCalendarDoesNotCancelKnownSessions()
    {
        await using var database = await TestDatabase.CreateAsync();
        await using var db = database.Context();
        var (integration, trainer, location) = await Seed(db, "empty");
        var future = new Session { Title = "Plan", Trainer = trainer, Location = location, Status = "Planned", StartAt = DateTime.UtcNow.AddDays(3), EndAt = DateTime.UtcNow.AddDays(3).AddHours(1) };
        db.Add(future);
        await db.SaveChangesAsync();
        db.Add(new CalendarEventLink { SessionId = future.Id, CalendarIntegrationId = integration.Id, ExternalEventId = "known" });
        db.Add(new ExternalCalendarEvent { CalendarIntegrationId = integration.Id, ExternalEventId = "known", Subject = "Plan", StartAt = future.StartAt, EndAt = future.EndAt, SessionId = future.Id });
        await db.SaveChangesAsync();
        var service = Service(db, new GraphHandler(r => r.RequestUri!.AbsolutePath.EndsWith("/me/calendarView") ? "{\"value\":[]}" : null), integration.UserId);

        var result = await service.ReconcileAsync(new() { PastDays = 0, FutureDays = 30 });

        Assert.Single(result.Errors);
        Assert.Equal(0, result.MissingOutlookEventsMarkedDeleted);
        db.ChangeTracker.Clear();
        Assert.Equal("Planned", (await db.Sessions.SingleAsync()).Status);
        Assert.Equal(1, await db.CalendarEventLinks.CountAsync());
    }

    [PostgresFact]
    public async Task PastEventIsReportedForReviewInsteadOfBecomingASettledSession()
    {
        await using var database = await TestDatabase.CreateAsync();
        await using var db = database.Context();
        var (integration, _, location) = await Seed(db, "past");
        var start = DateTime.UtcNow.AddDays(-2);
        var events = new[]
        {
            new
            {
                id = "past-1", subject = "Trening", bodyPreview = "", type = "singleInstance", seriesMasterId = (string?)null,
                categories = Array.Empty<string>(), transactionId = (string?)null,
                start = new { dateTime = start.ToString("yyyy-MM-ddTHH:mm:ss.fffffff"), timeZone = "UTC" },
                end = new { dateTime = start.AddHours(1).ToString("yyyy-MM-ddTHH:mm:ss.fffffff"), timeZone = "UTC" },
                location = new { displayName = location.Name },
                organizer = new { emailAddress = new { address = integration.Email } },
                attendees = Array.Empty<object>()
            }
        };
        var handler = new GraphHandler(r => r.RequestUri!.AbsolutePath.EndsWith("/me/calendarView") ? JsonSerializer.Serialize(new { value = events }) : null);
        var service = Service(db, handler, integration.UserId, importFrom: "2026-01-01");

        await service.ReconcileAsync(new() { PastDays = 7, FutureDays = 1 });

        Assert.Equal(0, await db.Sessions.CountAsync());
        var evt = await db.ExternalCalendarEvents.SingleAsync();
        Assert.Contains(OutlookReviewIssues.PastEventNeedsReview(), JsonSerializer.Deserialize<List<string>>(evt.MappingWarningsJson!)!);
    }

    // 28.10 Weronika S: the event was attached to another session at the same time, that session was deleted in
    // CRM, and the event stayed "converted" without a session, so no refresh ever brought it back.
    [PostgresFact]
    public async Task EventWhoseSessionWasDeletedIsImportedAgain()
    {
        await using var database = await TestDatabase.CreateAsync();
        await using var db = database.Context();
        var (integration, trainer, location) = await Seed(db, "orphan");
        var client = new Client { FirstName = "Weronika", LastName = "Szewczyk", Email = "orphan-client@test.invalid", Trainer = trainer, Location = location };
        db.Add(client);
        var start = DateTime.UtcNow.Date.AddDays(2).AddHours(9);
        db.Add(new ExternalCalendarEvent
        {
            CalendarIntegrationId = integration.Id, ExternalEventId = "orphan-1", Subject = "Weronika S", StartAt = start, EndAt = start.AddHours(1),
            IsConvertedToSession = true, SessionId = null
        });
        await db.SaveChangesAsync();
        var events = new[]
        {
            new
            {
                id = "orphan-1", subject = "Weronika S", bodyPreview = "", type = "singleInstance", seriesMasterId = (string?)null,
                categories = Array.Empty<string>(), transactionId = (string?)null,
                start = new { dateTime = start.ToString("yyyy-MM-ddTHH:mm:ss.fffffff"), timeZone = "UTC" },
                end = new { dateTime = start.AddHours(1).ToString("yyyy-MM-ddTHH:mm:ss.fffffff"), timeZone = "UTC" },
                location = new { displayName = location.Name },
                organizer = new { emailAddress = new { address = integration.Email } },
                attendees = new[] { new { emailAddress = new { address = client.Email } } }
            }
        };
        var handler = new GraphHandler(r => r.RequestUri!.AbsolutePath.EndsWith("/me/calendarView")
            ? JsonSerializer.Serialize(new { value = events })
            : r.Method == HttpMethod.Patch ? "{}" : null);
        var service = Service(db, handler, integration.UserId, importFrom: "2026-01-01");

        await service.ReconcileAsync(new() { PastDays = 0, FutureDays = 7 });

        db.ChangeTracker.Clear();
        var evt = await db.ExternalCalendarEvents.SingleAsync();
        Assert.NotNull(evt.SessionId);
        Assert.True(evt.IsConvertedToSession);
        Assert.Equal(client.Id, (await db.SessionParticipants.SingleAsync(p => p.SessionId == evt.SessionId)).ClientId);
    }

    [PostgresFact]
    public async Task OwnerSeesReviewIssuesOfTrainerCalendarsButNotTheirOtherWarnings()
    {
        await using var database = await TestDatabase.CreateAsync();
        await using var db = database.Context();
        var (integration, _, _) = await Seed(db, "issues");
        var owner = new User { Email = "issues-owner@test.invalid" };
        db.Add(owner);
        db.Add(new ExternalCalendarEvent
        {
            CalendarIntegrationId = integration.Id, ExternalEventId = "x", Subject = "S", StartAt = DateTime.UtcNow, EndAt = DateTime.UtcNow,
            MappingWarningsJson = JsonSerializer.Serialize(new[] { OutlookReviewIssues.CompletedSessionDeleted(), "Nie znaleziono klienta: a@b.pl" })
        });
        await db.SaveChangesAsync();

        var ownerIssues = await new ExternalCalendarEventService(db, new PortalSecurityTests.Actor(owner.Id, "Owner"), null!).GetIssuesAsync();
        var ownerIssue = Assert.Single(ownerIssues);
        Assert.Equal((OutlookReviewIssues.CompletedSessionDeletedType, true), (ownerIssue.IssueType, ownerIssue.RequiresDecision));
        Assert.Null(ownerIssue.Email);
        Assert.Equal(2, (await new ExternalCalendarEventService(db, new PortalSecurityTests.Actor(integration.UserId), null!).GetIssuesAsync()).Count);
    }

    private static async Task<(CalendarIntegration, Trainer, Location)> Seed(StudioCRMDbContext db, string name)
    {
        var trainer = new Trainer { User = new User { Email = $"{name}-trainer@test.invalid" } };
        var location = new Location { Name = $"Studio {name}", City = "Test" };
        var integration = new CalendarIntegration
        {
            User = trainer.User, Provider = "Outlook", IsActive = true, Email = trainer.User.Email,
            AccessToken = "token", RefreshToken = "refresh", AccessTokenExpiresAt = DateTime.UtcNow.AddHours(1)
        };
        db.Add(new UserRole { User = trainer.User, Role = new Role { Name = "Trainer" } });
        db.AddRange(trainer, location, integration, new TrainerLocation { Trainer = trainer, Location = location });
        await db.SaveChangesAsync();
        return (integration, trainer, location);
    }

    private static OutlookWebhookService Service(StudioCRMDbContext db, GraphHandler handler, int userId, string? importFrom = null)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Outlook:ImportSessionsFrom"] = importFrom
        }).Build();
        return new OutlookWebhookService(db, new HttpClient(handler), configuration, new NoRefresh(), null!, new PortalSecurityTests.Actor(userId));
    }

    private static Task Invoke(OutlookWebhookService service, string method, params object[] args) =>
        (Task)typeof(OutlookWebhookService).GetMethod(method, BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(service, args)!;

    private static async Task<List<string>> Warnings(StudioCRMDbContext db, int sessionId) =>
        (await db.ExternalCalendarEvents.Where(e => e.SessionId == sessionId).Select(e => e.MappingWarningsJson).ToListAsync())
            .SelectMany(json => json == null ? [] : JsonSerializer.Deserialize<List<string>>(json)!).ToList();

    private sealed class NoRefresh : IOutlookTokenService
    {
        public Task EnsureValidAccessTokenAsync(CalendarIntegration integration, bool forceRefresh = false) => Task.CompletedTask;
    }

    private sealed class GraphHandler(Func<HttpRequestMessage, string?> respond) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var body = respond(request);
            return Task.FromResult(body is null
                ? new HttpResponseMessage(HttpStatusCode.NotFound)
                : new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(body, Encoding.UTF8, "application/json") });
        }
    }
}
