using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using StudioCRM.Application.Interfaces.Calendar;
using StudioCRM.Domain.Entities;
using StudioCRM.Infrastructure.Services.Calendar;

namespace StudioCRM.Tests.IntegrationTests;

public class OutlookSeriesOccurrenceDeleteTests
{
    // Deleting one occurrence of a series only notifies an update of the series master. The planned session of the
    // deleted occurrence used to stay until a manual reconcile.
    [PostgresFact]
    public async Task DeletedOccurrenceCancelsItsPlannedSessionOnASeriesUpdate()
    {
        await using var database = await TestDatabase.CreateAsync();
        await using var db = database.Context();
        var trainer = new Trainer { User = new User { Email = "series-delete@example.test" } };
        var location = new Location { Name = "Kłaj" };
        var integration = new CalendarIntegration
        {
            User = trainer.User, Provider = "Outlook", IsActive = true, Email = trainer.User.Email,
            AccessToken = "token", RefreshToken = "refresh", AccessTokenExpiresAt = DateTime.UtcNow.AddHours(1)
        };
        var subscription = new CalendarSubscription
        {
            CalendarIntegration = integration, SubscriptionId = "series-sub", ClientState = new string('a', 64), ExpiresAt = DateTime.UtcNow.AddDays(1)
        };
        var start = DateTime.UtcNow.Date.AddDays(5).AddHours(16);
        Session Planned(int week) => new()
        {
            Title = "Aleksandra K + Aneta N", Trainer = trainer, Location = location, Status = "Planned",
            StartAt = start.AddDays(7 * week), EndAt = start.AddDays(7 * week).AddHours(1)
        };
        var kept = Planned(0);
        var deleted = Planned(1);
        db.AddRange(trainer, location, integration, subscription, kept, deleted);
        await db.SaveChangesAsync();
        foreach (var (id, session) in new[] { ("occ-1", kept), ("occ-2", deleted) })
        {
            db.Add(new ExternalCalendarEvent
            {
                CalendarIntegrationId = integration.Id, Provider = "Outlook", ExternalEventId = id, Subject = session.Title,
                StartAt = session.StartAt, EndAt = session.EndAt, SeriesMasterId = "master", IsRecurring = true,
                SessionId = session.Id, IsConvertedToSession = true
            });
            db.Add(new CalendarEventLink { SessionId = session.Id, CalendarIntegrationId = integration.Id, Provider = "Outlook", ExternalEventId = id });
        }
        await db.SaveChangesAsync();

        var instancesFail = true;
        object Event(string id, string type, DateTime at) => new
        {
            id, subject = "Aleksandra K + Aneta N", bodyPreview = "", type, seriesMasterId = type == "seriesMaster" ? null : "master",
            categories = Array.Empty<string>(), transactionId = (string?)null,
            start = new { dateTime = at.ToString("yyyy-MM-ddTHH:mm:ss.fffffff"), timeZone = "UTC" },
            end = new { dateTime = at.AddHours(1).ToString("yyyy-MM-ddTHH:mm:ss.fffffff"), timeZone = "UTC" },
            location = new { displayName = "Kłaj" },
            organizer = new { emailAddress = new { address = trainer.User.Email } },
            attendees = Array.Empty<object>()
        };
        var handler = new GraphHandler(request =>
        {
            var path = request.RequestUri!.AbsolutePath;
            if (path.EndsWith("/me/events/master/instances"))
                return instancesFail ? (HttpStatusCode.InternalServerError, "{}") : (HttpStatusCode.OK, JsonSerializer.Serialize(new { value = new[] { new { id = "occ-1" } } }));
            if (path.EndsWith("/me/events/master")) return (HttpStatusCode.OK, JsonSerializer.Serialize(Event("master", "seriesMaster", start)));
            if (path.EndsWith("/me/events/occ-1")) return (HttpStatusCode.OK, JsonSerializer.Serialize(Event("occ-1", "occurrence", start)));
            if (path.EndsWith("/me/outlook/masterCategories")) return (HttpStatusCode.OK, "{\"value\":[]}");
            return (HttpStatusCode.NotFound, "{}");
        });
        var service = new OutlookWebhookService(db, new HttpClient(handler), new ConfigurationBuilder().AddInMemoryCollection().Build(),
            new NoRefresh(), null!, new PortalSecurityTests.Actor(trainer.UserId));
        var notification = JsonSerializer.Serialize(new { value = new[] { new
        {
            subscriptionId = subscription.SubscriptionId, clientState = subscription.ClientState, resource = "me/events/master", changeType = "updated"
        } } });

        // A failed instances read must not look like deleted occurrences.
        await service.HandleNotificationAsync(notification);
        db.ChangeTracker.Clear();
        Assert.Equal("Planned", (await db.Sessions.SingleAsync(s => s.Id == deleted.Id)).Status);

        instancesFail = false;
        await service.HandleNotificationAsync(notification);
        db.ChangeTracker.Clear();

        Assert.Equal("Cancelled", (await db.Sessions.SingleAsync(s => s.Id == deleted.Id)).Status);
        Assert.Equal("Planned", (await db.Sessions.SingleAsync(s => s.Id == kept.Id)).Status);
        Assert.StartsWith("[DELETED]", (await db.ExternalCalendarEvents.SingleAsync(e => e.ExternalEventId == "occ-2")).Subject);
        Assert.False(await db.CalendarEventLinks.AnyAsync(l => l.SessionId == deleted.Id));
        Assert.True(await db.CalendarEventLinks.AnyAsync(l => l.SessionId == kept.Id));
    }

    // Graph sends notifications in batches. One event that failed (here: Graph throws) used to drop the rest of the
    // batch, and the webhook had already answered, so those changes waited for a manual reconcile.
    [PostgresFact]
    public async Task FailingNotificationDoesNotDropTheRestOfTheBatch()
    {
        await using var database = await TestDatabase.CreateAsync();
        await using var db = database.Context();
        var trainer = new Trainer { User = new User { Email = "batch-failure@example.test" } };
        var location = new Location { Name = "Kłaj" };
        var integration = new CalendarIntegration
        {
            User = trainer.User, Provider = "Outlook", IsActive = true, Email = trainer.User.Email,
            AccessToken = "token", RefreshToken = "refresh", AccessTokenExpiresAt = DateTime.UtcNow.AddHours(1)
        };
        var subscription = new CalendarSubscription
        {
            CalendarIntegration = integration, SubscriptionId = "batch-sub", ClientState = new string('c', 64), ExpiresAt = DateTime.UtcNow.AddDays(1)
        };
        var start = DateTime.UtcNow.Date.AddDays(3).AddHours(12);
        var session = new Session { Title = "Weronika S", Trainer = trainer, Location = location, Status = "Planned", StartAt = start, EndAt = start.AddHours(1) };
        db.AddRange(trainer, location, integration, subscription, session);
        await db.SaveChangesAsync();
        db.Add(new ExternalCalendarEvent
        {
            CalendarIntegrationId = integration.Id, Provider = "Outlook", ExternalEventId = "gone", Subject = session.Title,
            StartAt = start, EndAt = start.AddHours(1), SessionId = session.Id, IsConvertedToSession = true
        });
        db.Add(new CalendarEventLink { SessionId = session.Id, CalendarIntegrationId = integration.Id, Provider = "Outlook", ExternalEventId = "gone" });
        await db.SaveChangesAsync();

        var handler = new GraphHandler(_ => throw new HttpRequestException("Graph unavailable"));
        var service = new OutlookWebhookService(db, new HttpClient(handler), new ConfigurationBuilder().AddInMemoryCollection().Build(),
            new NoRefresh(), null!, new PortalSecurityTests.Actor(trainer.UserId));
        var batch = JsonSerializer.Serialize(new { value = new[]
        {
            new { subscriptionId = subscription.SubscriptionId, clientState = subscription.ClientState, resource = "me/events/broken", changeType = "updated" },
            new { subscriptionId = subscription.SubscriptionId, clientState = subscription.ClientState, resource = "me/events/gone", changeType = "deleted" }
        } });

        // The failure is still reported (the worker logs it), but only after the whole batch was handled.
        await Assert.ThrowsAsync<HttpRequestException>(() => service.HandleNotificationAsync(batch));
        db.ChangeTracker.Clear();

        Assert.Equal("Cancelled", (await db.Sessions.SingleAsync(s => s.Id == session.Id)).Status);
    }

    private sealed class NoRefresh : IOutlookTokenService
    {
        public Task EnsureValidAccessTokenAsync(CalendarIntegration integration, bool forceRefresh = false) => Task.CompletedTask;
    }

    private sealed class GraphHandler(Func<HttpRequestMessage, (HttpStatusCode Status, string Body)> respond) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var (status, body) = respond(request);
            return Task.FromResult(new HttpResponseMessage(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") });
        }
    }
}
