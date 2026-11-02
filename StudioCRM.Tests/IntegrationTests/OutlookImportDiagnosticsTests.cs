using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using StudioCRM.Application.Interfaces.Calendar;
using StudioCRM.Domain.Entities;
using StudioCRM.Infrastructure.Services.Calendar;

namespace StudioCRM.Tests.IntegrationTests;

public class OutlookImportDiagnosticsTests
{
    [PostgresFact]
    public async Task OwnerSeesWhyEachEventOfATrainerCalendarIsOrIsNotASession()
    {
        await using var database = await TestDatabase.CreateAsync();
        await using var db = database.Context();
        var owner = new User { Email = "owner-diag@example.test" };
        var location = new Location { Name = "Kłaj" };
        var bartosz = new Trainer { User = new User { Email = "bartosz-diag@example.test", FirstName = "Bartosz", LastName = "C" } };
        var karolina = new Trainer { User = new User { Email = "karolina-diag@example.test", FirstName = "Karolina", LastName = "W" } };
        CalendarIntegration Outlook(Trainer trainer) => new()
        {
            User = trainer.User, Provider = "Outlook", IsActive = true, Email = trainer.User.Email,
            AccessToken = "token", RefreshToken = "refresh", AccessTokenExpiresAt = DateTime.UtcNow.AddHours(1)
        };
        var bartoszOutlook = Outlook(bartosz);
        var karolinaOutlook = Outlook(karolina);
        var start = DateTime.UtcNow.Date.AddDays(3).AddHours(9);
        // Moved to Karolina: her calendar holds the session's event, Bartosz's old row still points at the session.
        var moved = new Session { Title = "Magdalena M", Trainer = karolina, Location = location, StartAt = start, EndAt = start.AddHours(1) };
        db.AddRange(owner, location, bartoszOutlook, karolinaOutlook, moved,
            new CalendarSubscription { CalendarIntegration = bartoszOutlook, SubscriptionId = "sub", ExpiresAt = DateTime.UtcNow.AddDays(2) });
        await db.SaveChangesAsync();
        db.AddRange(
            new CalendarEventLink { SessionId = moved.Id, CalendarIntegrationId = karolinaOutlook.Id, Provider = "Outlook", ExternalEventId = "karolina-copy" },
            new ExternalCalendarEvent { CalendarIntegrationId = bartoszOutlook.Id, Provider = "Outlook", ExternalEventId = "stale", Subject = "Magdalena M",
                StartAt = start, EndAt = start.AddHours(1), SessionId = moved.Id, IsConvertedToSession = true },
            new ExternalCalendarEvent { CalendarIntegrationId = bartoszOutlook.Id, Provider = "Outlook", ExternalEventId = "orphan", Subject = "Laura - Konsultacja",
                StartAt = start.AddDays(1), EndAt = start.AddDays(1).AddHours(1) },
            new ExternalCalendarEvent { CalendarIntegrationId = bartoszOutlook.Id, Provider = "Outlook", ExternalEventId = "warned", Subject = "Ktoś",
                StartAt = start.AddDays(2), EndAt = start.AddDays(2).AddHours(1), MappingWarningsJson = "[\"Nie znaleziono klienta: x@y.pl\"]" });
        await db.SaveChangesAsync();
        var rowsBefore = await db.ExternalCalendarEvents.CountAsync();

        object Event(string id, DateTime at, string locationName, string? transactionId = null, string type = "singleInstance") => new
        {
            id, subject = id, bodyPreview = "", type, seriesMasterId = (string?)null, categories = new[] { "Bartosz C" }, transactionId,
            start = new { dateTime = at.ToString("yyyy-MM-ddTHH:mm:ss.fffffff"), timeZone = "UTC" },
            end = new { dateTime = at.AddHours(1).ToString("yyyy-MM-ddTHH:mm:ss.fffffff"), timeZone = "UTC" },
            location = new { displayName = locationName },
            organizer = new { emailAddress = new { address = bartosz.User.Email } },
            attendees = Array.Empty<object>()
        };
        var events = new[]
        {
            Event("stale", start, "Kłaj"),
            Event("orphan", start.AddDays(1), "Kłaj; Kłaj237_Studio", "studiocrm-session-9-abc"),
            Event("warned", start.AddDays(2), "Kłaj"),
            Event("new", start.AddDays(3), "Kłaj"),
            Event("elsewhere", start.AddDays(4), "Kawiarnia")
        };
        var calls = 0;
        var handler = new GraphHandler(request =>
        {
            calls++;
            return request.RequestUri!.AbsolutePath.EndsWith("/me/calendarView") ? JsonSerializer.Serialize(new { value = events }) : null;
        });
        OutlookWebhookService Service(int userId, string role) => new(db, new HttpClient(handler),
            new ConfigurationBuilder().AddInMemoryCollection().Build(), new NoRefresh(), null!, new PortalSecurityTests.Actor(userId, role));

        var result = await Service(owner.Id, "Owner").DiagnoseImportAsync(new() { TrainerId = bartosz.Id });

        Assert.True(result.IntegrationActive);
        Assert.True(result.SubscriptionActive);
        var byId = result.Events.ToDictionary(e => e.ExternalEventId);
        Assert.Equal("StaleCopy", byId["stale"].Code);
        Assert.True(byId["stale"].LinkedElsewhere);
        Assert.Equal("Karolina W", byId["stale"].SessionTrainerName);
        Assert.Equal("CrmEventWithoutSession", byId["orphan"].Code);
        Assert.True(byId["orphan"].KnownLocation);
        Assert.Equal("MappingWarning", byId["warned"].Code);
        Assert.Equal("NotReceived", byId["new"].Code);
        Assert.Equal("UnknownLocation", byId["elsewhere"].Code);
        Assert.Equal(1, result.Summary["StaleCopy"]);
        // Read-only: nothing imported, nothing changed.
        Assert.Equal(rowsBefore, await db.ExternalCalendarEvents.CountAsync());
        Assert.Equal(1, await db.Sessions.CountAsync());

        // Karolina's own copy is the session's event.
        var karolinaView = await Service(owner.Id, "Owner").DiagnoseImportAsync(new() { TrainerId = karolina.Id });
        Assert.Equal("NotReceived", Assert.Single(karolinaView.Events, e => e.ExternalEventId == "stale").Code);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            Service(bartosz.UserId, "Trainer").DiagnoseImportAsync(new() { TrainerId = bartosz.Id }));
        await Assert.ThrowsAsync<InvalidOperationException>(() => Service(owner.Id, "Owner").DiagnoseImportAsync(
            new() { TrainerId = bartosz.Id, From = start, To = start.AddDays(63) }));
        await Assert.ThrowsAsync<KeyNotFoundException>(() => Service(owner.Id, "Owner").DiagnoseImportAsync(new() { TrainerId = int.MaxValue }));

        var unconnected = new Trainer { User = new User { Email = "nobody-diag@example.test" } };
        db.Add(unconnected);
        await db.SaveChangesAsync();
        calls = 0;
        var none = await Service(owner.Id, "Owner").DiagnoseImportAsync(new() { TrainerId = unconnected.Id });
        Assert.False(none.IntegrationActive);
        Assert.Empty(none.Events);
        Assert.Equal(0, calls);
    }

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
