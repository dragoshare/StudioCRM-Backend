using System.Net;
using System.Reflection;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using StudioCRM.Application.Common;
using StudioCRM.Application.Interfaces.Calendar;
using StudioCRM.Domain.Entities;
using StudioCRM.Infrastructure.Services.Calendar;

namespace StudioCRM.Tests.IntegrationTests;

// 30.10: a trainer gave today's consultation to another trainer by setting her category in Outlook. The trainer's
// calendar account could not "reassign this trainer", the update threw and CRM kept the original trainer.
public class OutlookTrainerHandOverTests
{
    [PostgresFact]
    public async Task TrainerCalendarHandsOwnSessionToCategoryTrainerButCannotTakeAnother()
    {
        await using var database = await TestDatabase.CreateAsync();
        await using var db = database.Context();
        var location = new Location { Name = "Hand-over studio", City = "Test" };
        var anna = new Trainer { User = new User { Email = "handover-anna@test.invalid", FirstName = "Anna", LastName = "Trener" }, OutlookCategoryName = "Anna" };
        var kasia = new Trainer { User = new User { Email = "handover-kasia@test.invalid", FirstName = "Kasia", LastName = "Trener" }, OutlookCategoryName = "Kasia" };
        var integration = new CalendarIntegration
        {
            User = anna.User, Provider = "Outlook", IsActive = true, Email = anna.User.Email,
            AccessToken = "token", RefreshToken = "refresh", AccessTokenExpiresAt = DateTime.UtcNow.AddHours(1)
        };
        var start = DateTime.UtcNow.AddHours(3);
        var consultation = new Session
        {
            Title = ConsultationSessions.DefaultTitle, Trainer = anna, Location = location, StartAt = start, EndAt = start.AddHours(1),
            PlannedSessionType = ConsultationSessions.SessionType
        };
        var kasiasSession = new Session
        {
            Title = ConsultationSessions.DefaultTitle, Trainer = kasia, Location = location, StartAt = start.AddHours(2), EndAt = start.AddHours(3),
            PlannedSessionType = ConsultationSessions.SessionType
        };
        var trainerRole = new Role { Name = "Trainer" };
        db.AddRange(new UserRole { User = anna.User, Role = trainerRole }, new UserRole { User = kasia.User, Role = trainerRole });
        db.AddRange(integration, consultation, kasiasSession,
            new TrainerLocation { Trainer = anna, Location = location }, new TrainerLocation { Trainer = kasia, Location = location });
        await db.SaveChangesAsync();
        ExternalCalendarEvent Event(string id, Session session, string category) => new()
        {
            CalendarIntegrationId = integration.Id, ExternalEventId = id, Subject = "Konsultacja", StartAt = session.StartAt, EndAt = session.EndAt,
            OrganizerEmail = anna.User.Email, SessionId = session.Id, IsConvertedToSession = true, AttendeesJson = "[]",
            CategoriesJson = JsonSerializer.Serialize(new[] { ConsultationSessions.OutlookCategory, category })
        };
        var handOver = Event("evt-handover", consultation, "Kasia");
        var takeOver = Event("evt-takeover", kasiasSession, "Anna");
        db.AddRange(handOver, takeOver);
        await db.SaveChangesAsync();

        var service = new OutlookWebhookService(db, new HttpClient(new OkHandler()), new ConfigurationBuilder().Build(), new NoRefresh(), null!,
            new PortalSecurityTests.Actor(anna.UserId));
        Task Update(ExternalCalendarEvent evt) => (Task)typeof(OutlookWebhookService)
            .GetMethod("UpdateSessionFromOutlookAsync", BindingFlags.NonPublic | BindingFlags.Instance)!
            .Invoke(service, [integration, evt, evt.ExternalEventId, new List<string>()])!;

        await Update(handOver);
        await Update(takeOver);

        Assert.Equal(kasia.Id, consultation.TrainerId);
        Assert.Equal(ConsultationSessions.SessionType, consultation.PlannedSessionType);
        // Anna's calendar does not manage Kasia's session, so Anna's category cannot pull it over.
        Assert.Equal(kasia.Id, kasiasSession.TrainerId);
    }

    private sealed class NoRefresh : IOutlookTokenService
    {
        public Task EnsureValidAccessTokenAsync(CalendarIntegration integration, bool forceRefresh = false) => Task.CompletedTask;
    }

    private sealed class OkHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{}", Encoding.UTF8, "application/json") });
    }
}
