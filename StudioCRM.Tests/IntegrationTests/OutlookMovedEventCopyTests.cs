using System.Net;
using System.Reflection;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using StudioCRM.Application.Interfaces.Calendar;
using StudioCRM.Application.Settings;
using StudioCRM.Domain.Entities;
using StudioCRM.Infrastructure.Persistence;
using StudioCRM.Infrastructure.Services.Calendar;

namespace StudioCRM.Tests.IntegrationTests;

// 2.11.2026: session #1051 moved from Bartosz to Karolina. Bartosz's import row kept the session, so deleting or
// moving that copy in his Outlook would cancel or move the session, and cancelling it in CRM left the copy behind.
public class OutlookMovedEventCopyTests
{
    [PostgresFact]
    public async Task MovingSessionToAnotherCalendarUnlinksTheOldCopySoItCannotChangeTheSession()
    {
        await using var database = await TestDatabase.CreateAsync();
        await using var db = database.Context();
        var (bartoszOutlook, karolinaOutlook, session) = await SeedMovedSessionAsync(db);
        db.AddRange(
            new CalendarEventLink { SessionId = session.Id, CalendarIntegrationId = bartoszOutlook.Id, Provider = "Outlook", ExternalEventId = "bartosz-evt" },
            Imported(bartoszOutlook, "bartosz-evt", session));
        await db.SaveChangesAsync();
        var graph = new GraphHandler();

        await SyncService(db, graph).SyncSessionAsync(session.Id);

        var link = await db.CalendarEventLinks.SingleAsync(l => l.SessionId == session.Id);
        Assert.Equal((karolinaOutlook.Id, "karolina-evt"), (link.CalendarIntegrationId, link.ExternalEventId));
        var old = await db.ExternalCalendarEvents.SingleAsync(e => e.ExternalEventId == "bartosz-evt");
        Assert.Null(old.SessionId);
        Assert.False(old.IsConvertedToSession);
        Assert.StartsWith("[DELETED]", old.Subject);
        Assert.Equal(session.Id, (await db.ExternalCalendarEvents.SingleAsync(e => e.ExternalEventId == "karolina-evt")).SessionId);
        // The new event exists before the old one is deleted.
        Assert.Equal(new[] { "POST /v1.0/me/events", "DELETE /v1.0/me/events/bartosz-evt" }, graph.Requests);

        var webhook = WebhookService(db, karolinaOutlook.UserId);
        // Outlook reports the deletion of the old event: the session stays.
        await MarkDeleted(webhook, bartoszOutlook.Id, "bartosz-evt");
        Assert.Equal("Planned", (await db.Sessions.AsNoTracking().SingleAsync(s => s.Id == session.Id)).Status);

        // A leftover from before the fix still points at the session: moving it does not move the session...
        var leftover = Imported(bartoszOutlook, "pre-fix-copy", session);
        db.Add(leftover);
        await db.SaveChangesAsync();
        leftover.StartAt = session.StartAt.AddHours(2);
        leftover.EndAt = session.EndAt.AddHours(2);
        await UpdateFromOutlook(webhook, bartoszOutlook, leftover);
        var reloaded = await db.Sessions.AsNoTracking().SingleAsync(s => s.Id == session.Id);
        Assert.Equal(session.StartAt, reloaded.StartAt);
        Assert.Equal(session.TrainerId, reloaded.TrainerId);
        // ...and deleting it neither cancels the session nor keeps pointing at it.
        await MarkDeleted(webhook, bartoszOutlook.Id, "pre-fix-copy");
        Assert.Equal("Planned", (await db.Sessions.AsNoTracking().SingleAsync(s => s.Id == session.Id)).Status);
        Assert.Null((await db.ExternalCalendarEvents.AsNoTracking().SingleAsync(e => e.ExternalEventId == "pre-fix-copy")).SessionId);
        Assert.True(await db.CalendarEventLinks.AnyAsync(l => l.SessionId == session.Id));

        // The session's own event still cancels it when deleted in Outlook.
        await MarkDeleted(webhook, karolinaOutlook.Id, "karolina-evt");
        Assert.Equal("Cancelled", (await db.Sessions.AsNoTracking().SingleAsync(s => s.Id == session.Id)).Status);
    }

    [PostgresFact]
    public async Task CancellingSessionInCrmDeletesEveryOutlookEventPointingAtIt()
    {
        await using var database = await TestDatabase.CreateAsync();
        await using var db = database.Context();
        var (bartoszOutlook, karolinaOutlook, session) = await SeedMovedSessionAsync(db);
        // As in production: a leftover copy in Bartosz's calendar next to the linked event in Karolina's.
        db.AddRange(
            new CalendarEventLink { SessionId = session.Id, CalendarIntegrationId = karolinaOutlook.Id, Provider = "Outlook", ExternalEventId = "karolina-evt" },
            Imported(karolinaOutlook, "karolina-evt", session),
            Imported(bartoszOutlook, "bartosz-evt", session));
        // #1051 after the cancellation: the link is gone, the copy stayed.
        var cancelled = new Session
        {
            Title = "Magdalena M", TrainerId = session.TrainerId, LocationId = session.LocationId, Status = "Cancelled",
            StartAt = session.StartAt.AddDays(1), EndAt = session.EndAt.AddDays(1)
        };
        db.Add(cancelled);
        await db.SaveChangesAsync();
        db.Add(Imported(bartoszOutlook, "bartosz-left", cancelled));
        await db.SaveChangesAsync();
        var graph = new GraphHandler();
        var sync = SyncService(db, graph);

        await sync.DeleteSessionEventAsync(session.Id);
        await sync.DeleteSessionEventAsync(cancelled.Id);

        Assert.Equal(
            new[] { "DELETE /v1.0/me/events/bartosz-evt", "DELETE /v1.0/me/events/bartosz-left", "DELETE /v1.0/me/events/karolina-evt" },
            graph.Requests.Order());
        Assert.False(await db.CalendarEventLinks.AnyAsync());
        Assert.All(await db.ExternalCalendarEvents.AsNoTracking().ToListAsync(), row =>
        {
            Assert.Null(row.SessionId);
            Assert.False(row.IsConvertedToSession);
            Assert.StartsWith("[DELETED]", row.Subject);
        });
    }

    private static async Task<(CalendarIntegration Bartosz, CalendarIntegration Karolina, Session Session)> SeedMovedSessionAsync(StudioCRMDbContext db)
    {
        var location = new Location { Name = "Kłaj" };
        var bartosz = new Trainer { User = new User { Email = "bartosz-moved@example.test", FirstName = "Bartosz", LastName = "C" } };
        var karolina = new Trainer { User = new User { Email = "karolina-moved@example.test", FirstName = "Karolina", LastName = "W" } };
        CalendarIntegration Outlook(Trainer trainer) => new()
        {
            User = trainer.User, Provider = "Outlook", IsActive = true, Email = trainer.User.Email,
            AccessToken = "token", RefreshToken = "refresh", AccessTokenExpiresAt = DateTime.UtcNow.AddHours(1)
        };
        var bartoszOutlook = Outlook(bartosz);
        var karolinaOutlook = Outlook(karolina);
        var start = DateTime.UtcNow.Date.AddDays(3).AddHours(18);
        // The trainer was already changed in CRM; the event is still in Bartosz's calendar.
        var session = new Session { Title = "Magdalena M", Trainer = karolina, Location = location, StartAt = start, EndAt = start.AddHours(1) };
        db.AddRange(location, bartoszOutlook, karolinaOutlook, session,
            new TrainerLocation { Trainer = bartosz, Location = location }, new TrainerLocation { Trainer = karolina, Location = location });
        await db.SaveChangesAsync();
        return (bartoszOutlook, karolinaOutlook, session);
    }

    private static ExternalCalendarEvent Imported(CalendarIntegration integration, string id, Session session) => new()
    {
        CalendarIntegrationId = integration.Id, Provider = "Outlook", ExternalEventId = id, Subject = session.Title,
        StartAt = session.StartAt, EndAt = session.EndAt, SessionId = session.Id, IsConvertedToSession = true,
        AttendeesJson = "[]", CategoriesJson = "[]"
    };

    private static OutlookCalendarSyncService SyncService(StudioCRMDbContext db, GraphHandler graph) =>
        new(db, Options.Create(new OutlookSettings()), new HttpClient(graph), NullLogger<OutlookCalendarSyncService>.Instance);

    private static OutlookWebhookService WebhookService(StudioCRMDbContext db, int userId) =>
        new(db, new HttpClient(new GraphHandler()), new ConfigurationBuilder().Build(), new NoRefresh(), null!,
            new PortalSecurityTests.Actor(userId));

    private static Task MarkDeleted(OutlookWebhookService service, int integrationId, string externalEventId) =>
        (Task)typeof(OutlookWebhookService).GetMethod("MarkDeletedAsync", BindingFlags.NonPublic | BindingFlags.Instance)!
            .Invoke(service, [integrationId, externalEventId])!;

    private static Task UpdateFromOutlook(OutlookWebhookService service, CalendarIntegration integration, ExternalCalendarEvent evt) =>
        (Task)typeof(OutlookWebhookService).GetMethod("UpdateSessionFromOutlookAsync", BindingFlags.NonPublic | BindingFlags.Instance)!
            .Invoke(service, [integration, evt, evt.ExternalEventId, new List<string>()])!;

    private sealed class NoRefresh : IOutlookTokenService
    {
        public Task EnsureValidAccessTokenAsync(CalendarIntegration integration, bool forceRefresh = false) => Task.CompletedTask;
    }

    private sealed class GraphHandler : HttpMessageHandler
    {
        public List<string> Requests { get; } = new();

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add($"{request.Method} {request.RequestUri!.AbsolutePath}");
            var body = request.Method == HttpMethod.Post ? "{\"id\":\"karolina-evt\"}" : "{}";
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(body, Encoding.UTF8, "application/json") });
        }
    }
}
