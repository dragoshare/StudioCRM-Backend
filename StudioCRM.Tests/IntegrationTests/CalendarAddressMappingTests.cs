using System.Reflection;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using StudioCRM.Domain.Entities;
using StudioCRM.Infrastructure.Services.Calendar;

namespace StudioCRM.Tests.IntegrationTests;

public class CalendarAddressMappingTests
{
    [PostgresFact]
    public async Task ImportRecognizesOfflineAliasAndLegacyRealEmailWithoutDuplicates()
    {
        await using var database = await TestDatabase.CreateAsync();
        await using var db = database.Context();
        var location = new Location { Name = "Calendar test", City = "Test", CalendarEmail = "room@example.test" };
        var trainer = new Trainer { User = new User { Email = "trainer@example.test" } };
        var integration = new CalendarIntegration { User = trainer.User };
        var offline = new Client { FirstName = "Anna", LastName = "Test", Location = location, Trainer = trainer };
        var registered = new Client { FirstName = "Jan", LastName = "Test", Email = "real@example.test", Location = location, Trainer = trainer };
        ClientCalendarAddress.Ensure(offline, "calendar.example.test");
        ClientCalendarAddress.Ensure(registered, "calendar.example.test");
        db.AddRange(location, trainer, integration, offline, registered);
        await db.SaveChangesAsync();
        var evt = new ExternalCalendarEvent
        {
            CalendarIntegrationId = integration.Id, ExternalEventId = "test-event", Provider = "Outlook",
            OrganizerEmail = trainer.User.Email, LocationEmail = location.CalendarEmail,
            StartAt = DateTime.UtcNow.AddDays(1), EndAt = DateTime.UtcNow.AddDays(1).AddHours(1),
            AttendeesJson = JsonSerializer.Serialize(new[] { offline.CalendarEmail!.ToUpperInvariant(), registered.Email, registered.CalendarEmail })
        };
        db.ExternalCalendarEvents.Add(evt);
        await db.SaveChangesAsync();
        var result = await new OutlookEventMapperService(db).MapToSessionAsync(evt);
        Assert.NotNull(result.Session);
        var ids = await db.SessionParticipants.Where(p => p.SessionId == result.Session!.Id).Select(p => p.ClientId).ToListAsync();
        Assert.Equal(2, ids.Count);
        Assert.Contains(offline.Id, ids);
        Assert.Contains(registered.Id, ids);

        // Webhook updates must resolve the same alias instead of removing the offline participant.
        evt.AttendeesJson = JsonSerializer.Serialize(new[] { offline.CalendarEmail });
        var webhook = new OutlookWebhookService(db, new HttpClient(), null!, null!, null!, null!);
        await (Task)typeof(OutlookWebhookService).GetMethod("SyncSessionParticipantsFromOutlookAsync", BindingFlags.NonPublic | BindingFlags.Instance)!
            .Invoke(webhook, new object[] { result.Session!, evt })!;
        Assert.Equal(offline.Id, await db.SessionParticipants.Where(p => p.SessionId == result.Session!.Id).Select(p => p.ClientId).SingleAsync());
        Assert.Equal("", offline.Email);
        Assert.Null(offline.UserId);
    }
}
