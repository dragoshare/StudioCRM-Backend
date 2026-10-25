using System.Net;
using System.Reflection;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using StudioCRM.Application.Interfaces.Calendar;
using StudioCRM.Domain.Entities;
using StudioCRM.Infrastructure.Services.Calendar;

namespace StudioCRM.Tests.IntegrationTests;

// The participant title is written back to Outlook once per attendee change made in Outlook. The echo of that write
// (same attendees) and a title typed by the trainer write nothing, so the 21.10 loop cannot come back.
public class OutlookTitleWriteBackTests
{
    [PostgresFact]
    public async Task AttendeeChangeWritesTitleOnceAndItsEchoWritesNothing()
    {
        await using var database = await TestDatabase.CreateAsync();
        await using var db = database.Context();
        var location = new Location { Name = "Title studio", City = "Test" };
        var trainer = new Trainer { User = new User { Email = "title-trainer@test.invalid", FirstName = "T", LastName = "Trener" } };
        var integration = new CalendarIntegration
        {
            User = trainer.User, Provider = "Outlook", IsActive = true, Email = trainer.User.Email,
            AccessToken = "token", RefreshToken = "refresh", AccessTokenExpiresAt = DateTime.UtcNow.AddHours(1)
        };
        var anna = new Client { FirstName = "Anna", LastName = "Kowal", Email = "anna-title@test.invalid", Trainer = trainer, Location = location };
        var beata = new Client { FirstName = "Beata", LastName = "Nowak", Email = "beata-title@test.invalid", Trainer = trainer, Location = location };
        var start = DateTime.UtcNow.AddDays(2);
        var session = new Session { Title = "Anna K", Trainer = trainer, Location = location, StartAt = start, EndAt = start.AddHours(1), PlannedSessionType = "OneToOne" };
        db.Add(new UserRole { User = trainer.User, Role = new Role { Name = "Trainer" } });
        db.AddRange(integration, anna, beata, session, new TrainerLocation { Trainer = trainer, Location = location },
            new SessionParticipant { Session = session, Client = anna });
        await db.SaveChangesAsync();
        var evt = new ExternalCalendarEvent
        {
            CalendarIntegrationId = integration.Id, ExternalEventId = "evt-title", Subject = "Anna K", StartAt = start, EndAt = start.AddHours(1),
            OrganizerEmail = trainer.User.Email, SessionId = session.Id, IsConvertedToSession = true,
            AttendeesJson = JsonSerializer.Serialize(new[] { anna.Email, beata.Email })
        };
        db.Add(evt);
        await db.SaveChangesAsync();

        var handler = new RecordingHandler();
        var service = new OutlookWebhookService(db, new HttpClient(handler), new ConfigurationBuilder().Build(), new NoRefresh(), null!,
            new PortalSecurityTests.Actor(trainer.UserId));
        Task Update(params string[] previous) => (Task)typeof(OutlookWebhookService)
            .GetMethod("UpdateSessionFromOutlookAsync", BindingFlags.NonPublic | BindingFlags.Instance)!
            .Invoke(service, [integration, evt, evt.ExternalEventId, previous.ToList()])!;

        // Beata added in Outlook: CRM title follows and is written to Outlook once.
        await Update(anna.Email);
        Assert.Equal(["Anna K + Beata N"], handler.Subjects);

        // Echo of our own PATCH: same attendees, nothing written.
        evt.Subject = "Anna K + Beata N";
        await Update(anna.Email, beata.Email);
        // Trainer renames the event in Outlook without changing attendees: kept, nothing written.
        evt.Subject = "Trening z Beatą";
        await Update(anna.Email, beata.Email);
        Assert.Single(handler.Subjects);
    }

    private sealed class NoRefresh : IOutlookTokenService
    {
        public Task EnsureValidAccessTokenAsync(CalendarIntegration integration, bool forceRefresh = false) => Task.CompletedTask;
    }

    private sealed class RecordingHandler : HttpMessageHandler
    {
        public List<string> Subjects { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (request.Method == HttpMethod.Patch)
                Subjects.Add(JsonDocument.Parse(await request.Content!.ReadAsStringAsync(cancellationToken)).RootElement.GetProperty("subject").GetString()!);
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{}", Encoding.UTF8, "application/json") };
        }
    }
}
