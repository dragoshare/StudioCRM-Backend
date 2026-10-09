using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using StudioCRM.Application.Interfaces;
using StudioCRM.Application.Interfaces.Calendar;
using StudioCRM.Application.DTOs.Sessions;
using StudioCRM.Domain.Entities;
using StudioCRM.Domain.Enums;
using StudioCRM.Infrastructure.Services;

namespace StudioCRM.Tests.IntegrationTests;

public class GroupBookingRulesTests
{
    [PostgresFact]
    public async Task RulesPersistAcrossLegacyUpdatesAndGroupMemberCanBeAddedOnlyToGroup()
    {
        await using var database = await TestDatabase.CreateAsync();
        await using var db = database.Context();
        var location = new Location { Name = "Rules", City = "Test" };
        var trainer = new Trainer { User = new User { Email = "rules-owner@example.test" } };
        var client = new Client { Location = location, FirstName = "Group", LastName = "Member",
            Email = "member@example.test" };
        db.AddRange(new TrainerLocation { Trainer = trainer, Location = location }, client);
        await db.SaveChangesAsync();
        var user = new TestUser(trainer.UserId, "Trainer");
        var sessions = new SessionService(db, user, null!, null!, new NoSync(), null!, NullLogger<SessionService>.Instance);
        var portal = new TrainerPortalService(db, user, null!, sessions, null!);
        var request = new CreateSessionDto
        {
            Title = "Group", LocationId = location.Id, PlannedSessionType = "Group", PublicCapacity = 8,
            StartAt = DateTime.UtcNow.AddDays(3), EndAt = DateTime.UtcNow.AddDays(3).AddHours(1),
            EventRules = "Bring water", RegistrationClosesBeforeMinutes = 45, CancellationClosesBeforeMinutes = 600,
            Participants = new() { new() { ClientId = client.Id, CountsAgainstPackage = false } }
        };
        await Assert.ThrowsAsync<InvalidOperationException>(() => portal.CreateSessionAsync(request));
        db.Add(new ClientLocationMembership { Client = client, Location = location, GroupAccessEnabled = true });
        await db.SaveChangesAsync();
        request.PlannedSessionType = "Individual";
        await Assert.ThrowsAsync<InvalidOperationException>(() => portal.CreateSessionAsync(request));
        request.PlannedSessionType = "Group";
        var created = await portal.CreateSessionAsync(request);
        Assert.Equal(8, created.Capacity);
        Assert.Equal(45, created.BookingRules.RegistrationClosesBeforeMinutes);
        var updated = await portal.UpdateSessionAsync(created.Id, new UpdateSessionDto
        {
            Title = "Group changed", LocationId = location.Id, PlannedSessionType = "Group", PublicCapacity = 8,
            StartAt = request.StartAt, EndAt = request.EndAt!.Value
        });
        Assert.NotNull(updated);
        Assert.Equal("Bring water", updated!.EventRules);
        Assert.Equal(600, updated.BookingRules.CancellationClosesBeforeMinutes);
        db.ChangeTracker.Clear();
        var stored = await db.Sessions.SingleAsync(s => s.Id == created.Id);
        Assert.Equal(45, stored.RegistrationClosesBeforeMinutes);
        Assert.Equal(600, stored.CancellationClosesBeforeMinutes);
    }

    [PostgresFact]
    public async Task PublicDeadlinesBlockMutationsAndTimelyCancellationReleasesEntry()
    {
        await using var database = await TestDatabase.CreateAsync();
        await using var db = database.Context();
        var trainerUser = new User { Email = "trainer-rules@example.test" };
        var clientUser = new User { Email = "client-rules@example.test", EmailVerifiedAt = DateTime.UtcNow };
        var location = new Location { Name = "Group studio", City = "Test" };
        var trainer = new Trainer { User = trainerUser };
        var client = new Client { User = clientUser, Trainer = trainer, Location = location,
            FirstName = "Anna", LastName = "Test", Email = clientUser.Email };
        var package = new Package { Name = "Group", BillingType = SessionBillingType.Group, Location = location };
        var pass = new ClientPackage { Client = client, Package = package, Location = location,
            Name = "Group", TotalSessions = 4, ExpectedBillingType = SessionBillingType.Group,
            PaymentStatus = PaymentStatus.Paid, IsActive = true, PurchaseDate = DateTime.UtcNow };
        var session = new Session { Title = "Group", Trainer = trainer, Location = location,
            StartAt = DateTime.UtcNow.AddMinutes(20), EndAt = DateTime.UtcNow.AddMinutes(80),
            IsPubliclyBookable = true, PlannedSessionType = "Group", PublicCapacity = 8,
            EventRules = "Bring water", RegistrationClosesBeforeMinutes = 30,
            CancellationClosesBeforeMinutes = 720 };
        db.AddRange(pass, session);
        await db.SaveChangesAsync();
        var service = new PublicGroupClassService(db, new TestUser(clientUser.Id, "Client"),
            new NoSync(), NullLogger<PublicGroupClassService>.Instance);

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => service.BookCurrentClientAsync(session.Id));
        Assert.Contains("deadline", error.Message);
        Assert.Empty(await db.SessionParticipants.ToListAsync());

        session.StartAt = DateTime.UtcNow.AddDays(2);
        session.EndAt = session.StartAt.AddHours(1);
        await db.SaveChangesAsync();
        var booking = await service.BookCurrentClientAsync(session.Id);
        Assert.Equal(3, booking.RemainingEntries);

        session.StartAt = DateTime.UtcNow.AddHours(2);
        session.EndAt = session.StartAt.AddHours(1);
        await db.SaveChangesAsync();
        error = await Assert.ThrowsAsync<InvalidOperationException>(() => service.CancelCurrentClientBookingAsync(session.Id));
        Assert.Contains("deadline", error.Message);
        Assert.Single(await db.SessionParticipants.ToListAsync());
        Assert.Equal(0, pass.UsedSessions);

        session.StartAt = DateTime.UtcNow.AddDays(2);
        session.EndAt = session.StartAt.AddHours(1);
        await db.SaveChangesAsync();
        Assert.True(await service.CancelCurrentClientBookingAsync(session.Id));
        Assert.Empty(await db.SessionParticipants.ToListAsync());
        Assert.Equal(0, pass.UsedSessions);
    }

    [PostgresFact]
    public async Task TrainerCalendarAndParticipantProfileAreScopedToAssignedLocation()
    {
        await using var database = await TestDatabase.CreateAsync();
        await using var db = database.Context();
        var location = new Location { Name = "Allowed", City = "Test" };
        var otherLocation = new Location { Name = "Other", City = "Test" };
        var trainer = new Trainer { User = new User { Email = "calendar-trainer@example.test" } };
        var otherTrainer = new Trainer { User = new User { Email = "other-trainer@example.test" } };
        var client = new Client { Location = location, Trainer = otherTrainer,
            FirstName = "Group", LastName = "Client", Email = "group-client@example.test" };
        var session = new Session { Trainer = otherTrainer, Location = location, Title = "Group",
            PlannedSessionType = "Group", PublicCapacity = 8, StartAt = DateTime.UtcNow.AddDays(1),
            EndAt = DateTime.UtcNow.AddDays(1).AddHours(1), EventRules = "Rules",
            RegistrationClosesBeforeMinutes = 30, CancellationClosesBeforeMinutes = 720 };
        var privateSession = new Session { Trainer = otherTrainer, Location = otherLocation,
            StartAt = session.StartAt, EndAt = session.EndAt, PlannedSessionType = "Group" };
        db.AddRange(new TrainerLocation { Trainer = trainer, Location = location },
            new SessionParticipant { Session = session, Client = client },
            new SessionParticipant { Session = privateSession, Client = client });
        await db.SaveChangesAsync();
        var portal = new TrainerPortalService(db, new TestUser(trainer.UserId, "Trainer"), null!, null!, null!);
        var calendar = await portal.GetSessionsAsync();
        var item = Assert.Single(calendar);
        Assert.True(item.IsGroupSession);
        Assert.False(item.CanEdit);
        Assert.Equal(1, item.BookedSeats);
        Assert.Equal(7, item.AvailableSeats);
        Assert.Equal("Rules", item.EventRules);
        Assert.Equal(client.Id, Assert.Single(item.Participants).ClientId);
        Assert.Equal(session.StartAt.AddMinutes(-30), item.BookingRules.RegistrationClosesAtUtc);
        Assert.NotNull(await portal.GetParticipantProfileAsync(session.Id, client.Id));
        Assert.Null(await portal.GetParticipantProfileAsync(privateSession.Id, client.Id));
        Assert.Null(await portal.GetParticipantProfileAsync(session.Id, client.Id + 1000));
    }

    private sealed class TestUser(int id, string role) : ICurrentUserService
    {
        public int? UserId => id;
        public string? Email => null;
        public List<string> Roles => new() { role };
        public bool IsAuthenticated => true;
        public bool IsOwner => role == "Owner";
        public bool IsTrainer => role == "Trainer";
        public bool IsClient => role == "Client";
    }

    private sealed class NoSync : IOutlookCalendarSyncService
    {
        public Task SyncSessionAsync(int sessionId) => Task.CompletedTask;
        public Task SyncSessionSeriesAsync(string recurringGroupId, SessionRecurrenceDto recurrence) => Task.CompletedTask;
        public Task DeleteSessionEventAsync(int sessionId) => Task.CompletedTask;
        public Task<bool> DeleteSessionSeriesAsync(string recurringGroupId) => Task.FromResult(false);
    }
}
