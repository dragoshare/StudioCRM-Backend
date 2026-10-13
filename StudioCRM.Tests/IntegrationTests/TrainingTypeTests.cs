using Microsoft.EntityFrameworkCore;
using StudioCRM.Application.DTOs.Clients;
using StudioCRM.Application.DTOs.Sessions;
using StudioCRM.Domain.Entities;
using StudioCRM.Domain.Enums;
using StudioCRM.Infrastructure.Services;

namespace StudioCRM.Tests.IntegrationTests;

public class TrainingTypeTests
{
    [PostgresFact]
    public async Task FourParticipantsRemainSemiPersonalOnCreateAndUpdate()
    {
        await using var database = await TestDatabase.CreateAsync();
        await using var db = database.Context();
        var location = new Location { Name = "Types", City = "Test" };
        var trainer = new Trainer { User = new User { Email = "types@example.test" } };
        var clients = Enumerable.Range(1, 4).Select(i => new Client { FirstName = "Client", LastName = i.ToString(), Location = location, Trainer = trainer }).ToList();
        db.Add(new TrainerLocation { Trainer = trainer, Location = location });
        db.AddRange(clients);
        await db.SaveChangesAsync();
        var service = PortalSecurityTests.Sessions(db, new PortalSecurityTests.Actor(trainer.UserId, "Owner"));
        var start = DateTime.UtcNow.AddDays(2);
        var created = await service.CreateAsync(new CreateSessionDto {
            TrainerId = trainer.Id, LocationId = location.Id, StartAt = start, EndAt = start.AddHours(1),
            Participants = clients.Select(c => new CreateSessionParticipantDto { ClientId = c.Id, CountsAgainstPackage = false }).ToList()
        });
        Assert.Equal("FourToOne", created.PlannedSessionType);
        var updated = await service.UpdateAsync(created.Id, new UpdateSessionDto {
            TrainerId = trainer.Id, LocationId = location.Id, StartAt = start, EndAt = start.AddHours(1)
        });
        Assert.Equal("FourToOne", updated!.PlannedSessionType);
    }

    [PostgresFact]
    public async Task ClientHistoryReturnsAllTypesAndPreservesMissingValues()
    {
        await using var database = await TestDatabase.CreateAsync();
        await using var db = database.Context();
        var location = new Location { Name = "History", City = "Test" };
        var trainer = new Trainer { User = new User { Email = "history-types@example.test" } };
        var client = new Client { FirstName = "Client", LastName = "History", Location = location, Trainer = trainer };
        foreach (var type in Enum.GetValues<SessionBillingType>())
            db.Add(new SessionParticipant { Client = client, PlannedBillingType = SessionBillingType.OneToOne, ActualBillingType = type,
                Session = new Session { Trainer = trainer, Location = location, Title = type.ToString(), Status = "Completed",
                    StartAt = DateTime.UtcNow.AddDays(-1), EndAt = DateTime.UtcNow, PlannedSessionType = "OneToOne", ActualSessionType = type.ToString() } });
        db.Add(new SessionParticipant { Client = client,
            Session = new Session { Trainer = trainer, Location = location, Title = "Missing", StartAt = DateTime.UtcNow, EndAt = DateTime.UtcNow.AddHours(1) } });
        await db.SaveChangesAsync();
        var service = new ClientService(db, new PortalSecurityTests.Actor(trainer.UserId, "Owner"), null!, null!);
        var history = await service.GetSessionHistoryAsync(client.Id, new ClientHistoryFilter());
        Assert.Equal(6, history.TotalCount);
        foreach (var type in Enum.GetValues<SessionBillingType>()) {
            var row = Assert.Single(history.Items.Where(x => x.Title == type.ToString()));
            Assert.Equal(type.ToString(), row.ActualSessionType);
            Assert.Equal(type.ToString(), row.ActualBillingType);
            Assert.Equal("OneToOne", row.PlannedSessionType);
            Assert.Equal("OneToOne", row.PlannedBillingType);
        }
        var missing = Assert.Single(history.Items.Where(x => x.Title == "Missing"));
        Assert.Null(missing.ActualSessionType);
        Assert.Null(missing.PlannedSessionType);
        Assert.Equal("", missing.ActualBillingType);
        Assert.Equal("", missing.PlannedBillingType);
    }
}
