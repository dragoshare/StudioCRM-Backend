using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using StudioCRM.Application.Interfaces.Calendar;
using StudioCRM.Domain.Entities;
using StudioCRM.Infrastructure.Services.Calendar;

namespace StudioCRM.Tests.IntegrationTests;

public class OutlookReconcileTests
{
    // Reconcile used to fetch every event again (GET /me/events/{id}) and the master categories once per event,
    // which made an owner's reconcile of all trainers take minutes. It must import from the calendar-view payload
    // and read the categories once per integration.
    [PostgresFact]
    public async Task ReconcileImportsFromCalendarViewWithoutPerEventRequests()
    {
        await using var database = await TestDatabase.CreateAsync();
        await using var db = database.Context();
        var owner = new User { Email = "owner-reconcile@example.test" };
        var integration = new CalendarIntegration
        {
            User = owner, Provider = "Outlook", IsActive = true, Email = owner.Email,
            AccessToken = "token", RefreshToken = "refresh", AccessTokenExpiresAt = DateTime.UtcNow.AddHours(1)
        };
        db.AddRange(owner, integration);
        await db.SaveChangesAsync();

        var start = DateTime.UtcNow.Date.AddDays(3).AddHours(9);
        var events = Enumerable.Range(1, 3).Select(i => new
        {
            id = $"evt-{i}", subject = $"Spotkanie {i}", bodyPreview = "", type = "singleInstance", seriesMasterId = (string?)null,
            categories = new[] { "Prywatne" }, transactionId = (string?)null,
            start = new { dateTime = start.AddDays(i).ToString("yyyy-MM-ddTHH:mm:ss.fffffff"), timeZone = "UTC" },
            end = new { dateTime = start.AddDays(i).AddHours(1).ToString("yyyy-MM-ddTHH:mm:ss.fffffff"), timeZone = "UTC" },
            location = new { displayName = "" },
            organizer = new { emailAddress = new { address = owner.Email } },
            attendees = Array.Empty<object>()
        }).ToArray();
        var handler = new GraphHandler(request =>
        {
            var path = request.RequestUri!.AbsolutePath;
            if (path.EndsWith("/me/calendarView")) return JsonSerializer.Serialize(new { value = events });
            if (path.EndsWith("/me/outlook/masterCategories")) return JsonSerializer.Serialize(new { value = new[] { new { displayName = "Prywatne", color = "preset1" } } });
            return null;
        });
        var configuration = new ConfigurationBuilder().AddInMemoryCollection().Build();
        var service = new OutlookWebhookService(db, new HttpClient(handler), configuration, new NoRefresh(), null!,
            new PortalSecurityTests.Actor(owner.Id, "Owner"));

        var result = await service.ReconcileAsync(new() { PastDays = 0, FutureDays = 30 });

        Assert.Empty(result.Errors);
        Assert.Equal((3, 3), (result.OutlookEventsFound, result.ImportedOrUpdatedEvents));
        Assert.Equal(3, await db.ExternalCalendarEvents.CountAsync());
        Assert.Equal(1, handler.Count("/me/calendarView"));
        Assert.Equal(1, handler.Count("/me/outlook/masterCategories"));
        Assert.Equal(0, handler.Count("/me/events/"));
    }

    [PostgresFact]
    public async Task OwnerCanLimitReconcileToOwnOrOneTrainersOutlook()
    {
        await using var database = await TestDatabase.CreateAsync();
        await using var db = database.Context();
        var owner = new User { Email = "owner-scope@example.test" };
        var trainer = new Trainer { User = new User { Email = "trainer-scope@example.test" } };
        CalendarIntegration Connect(User user) => new()
        {
            User = user, Provider = "Outlook", IsActive = true, Email = user.Email,
            AccessToken = "token", RefreshToken = "refresh", AccessTokenExpiresAt = DateTime.UtcNow.AddHours(1)
        };
        db.AddRange(owner, trainer, Connect(owner), Connect(trainer.User));
        await db.SaveChangesAsync();
        var configuration = new ConfigurationBuilder().AddInMemoryCollection().Build();
        OutlookWebhookService Service(GraphHandler handler, int userId, string role) =>
            new(db, new HttpClient(handler), configuration, new NoRefresh(), null!, new PortalSecurityTests.Actor(userId, role));
        GraphHandler Empty() => new(request => request.RequestUri!.AbsolutePath.EndsWith("/me/calendarView") ? "{\"value\":[]}" : null);

        Assert.Equal(2, (await Service(Empty(), owner.Id, "Owner").ReconcileAsync(new())).IntegrationsProcessed);
        Assert.Equal(1, (await Service(Empty(), owner.Id, "Owner").ReconcileAsync(new() { OnlyMine = true })).IntegrationsProcessed);
        var single = Empty();
        Assert.Equal(1, (await Service(single, owner.Id, "Owner").ReconcileAsync(new() { TrainerId = trainer.Id })).IntegrationsProcessed);
        Assert.Equal(1, single.Count("/me/calendarView"));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            Service(Empty(), trainer.UserId, "Trainer").ReconcileAsync(new() { TrainerId = trainer.Id }));
    }

    private sealed class NoRefresh : IOutlookTokenService
    {
        public Task EnsureValidAccessTokenAsync(CalendarIntegration integration, bool forceRefresh = false) => Task.CompletedTask;
    }

    private sealed class GraphHandler(Func<HttpRequestMessage, string?> respond) : HttpMessageHandler
    {
        private readonly List<string> paths = new();
        public int Count(string fragment) => paths.Count(p => p.Contains(fragment, StringComparison.Ordinal));

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            paths.Add(request.RequestUri!.AbsolutePath);
            var body = respond(request);
            return Task.FromResult(body is null
                ? new HttpResponseMessage(HttpStatusCode.NotFound)
                : new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(body, Encoding.UTF8, "application/json") });
        }
    }
}
