using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using StudioCRM.Application.DTOs.Alerts;
using StudioCRM.Application.DTOs.SessionParticipants;
using StudioCRM.Domain.Entities;
using StudioCRM.Domain.Enums;
using StudioCRM.Infrastructure.Services;

namespace StudioCRM.Tests.IntegrationTests;

public class UnsettledSessionAlertTests
{
    [PostgresFact]
    public async Task FailedAutoCompletionIsShownWithReasonToOwnerAndOwnTrainerAndClearsAfterFix()
    {
        await using var database = await TestDatabase.CreateAsync();
        await using var db = database.Context();
        var location = new Location { Name = "Alert studio" };
        var trainerUser = new User { Email = "alert-trainer@example.test", FirstName = "Ola", LastName = "Trener" };
        var trainer = new Trainer { User = trainerUser };
        var otherTrainer = new Trainer { User = new User { Email = "alert-other@example.test" } };
        var client = new Client { FirstName = "Marta", LastName = "Nowak", Location = location };
        var expired = new ClientPackage { Client = client, Package = new Package { Name = "Pakiet 8", BillingType = SessionBillingType.OneToOne, Price = 800, SessionsLimit = 8 },
            Name = "Pakiet 8 treningów", TotalSessions = 8, UsedSessions = 7, ExpectedBillingType = SessionBillingType.OneToOne, ExpectedUnitPrice = 100,
            IsActive = true, PurchaseDate = DateTime.UtcNow.AddDays(-40), ValidUntil = DateTime.UtcNow.AddDays(-2) };
        var session = new Session { Trainer = trainer, Location = location, Title = "Trening", StartAt = DateTime.UtcNow.AddHours(-3), EndAt = DateTime.UtcNow.AddHours(-2) };
        db.AddRange(expired, otherTrainer, new SessionParticipant { Session = session, Client = client });
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        var participants = new SessionParticipantService(db, null!, null!, null!, NullLogger<SessionParticipantService>.Instance);
        var worker = new SessionAutoCompletionService(db, participants, NullLogger<SessionAutoCompletionService>.Instance);
        var first = await worker.CompleteFinishedSessionsAsync();
        Assert.Equal(1, first.FailedCount);

        await using (var check = database.Context())
        {
            var stored = await check.Sessions.SingleAsync(s => s.Id == session.Id);
            Assert.Equal("Planned", stored.Status);
            Assert.NotNull(stored.AutoCompletionFailedAt);
            Assert.StartsWith("Marta Nowak: pakiet „Pakiet 8 treningów” był ważny do ", stored.AutoCompletionError);
        }

        // A recently failed session is not retried on every run.
        Assert.Equal(0, (await worker.CompleteFinishedSessionsAsync()).FailedCount);

        var owner = await new OperationalAlertService(db, new PortalSecurityTests.Actor(1, "Owner")).GetAlertsAsync(new OperationalAlertFilterDto { Type = "SessionNotSettled" });
        var ownerAlert = Assert.Single(owner.Items);
        Assert.Equal(session.Id, ownerAlert.SessionId);
        Assert.StartsWith("Marta Nowak: pakiet", ownerAlert.Message);
        Assert.DoesNotContain("poproś właściciela", ownerAlert.Message);

        var own = await new OperationalAlertService(db, new PortalSecurityTests.Actor(trainerUser.Id)).GetAlertsAsync(new OperationalAlertFilterDto { Type = "SessionNotSettled" });
        Assert.EndsWith("poproś właściciela studia.", Assert.Single(own.Items).Message);
        var other = await new OperationalAlertService(db, new PortalSecurityTests.Actor(otherTrainer.UserId)).GetAlertsAsync(new OperationalAlertFilterDto { Type = "SessionNotSettled" });
        Assert.Empty(other.Items);

        // Owner extends the package and completes the session: the reason and the alert disappear.
        await db.ClientPackages.Where(p => p.Id == expired.Id).ExecuteUpdateAsync(x => x.SetProperty(p => p.ValidUntil, DateTime.UtcNow.AddDays(30)));
        db.ChangeTracker.Clear();
        Assert.True(await participants.CompleteSessionAutomaticallyAsync(session.Id, new CompleteSessionDto { ActualSessionType = "OneToOne", Participants = new()
        {
            new() { ClientId = client.Id, AttendanceStatus = "Present", CountsAgainstPackage = true, SessionsCharged = 1 }
        }}));
        await using (var check = database.Context())
        {
            var stored = await check.Sessions.SingleAsync(s => s.Id == session.Id);
            Assert.Equal(("Completed", null, (string?)null), (stored.Status, stored.AutoCompletionFailedAt, stored.AutoCompletionError));
        }
        Assert.Empty((await new OperationalAlertService(db, new PortalSecurityTests.Actor(1, "Owner")).GetAlertsAsync(new OperationalAlertFilterDto { Type = "SessionNotSettled" })).Items);
    }
}
