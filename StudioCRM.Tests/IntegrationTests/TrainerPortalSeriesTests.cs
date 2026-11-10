using Microsoft.EntityFrameworkCore;
using StudioCRM.Application.DTOs.Sessions;
using StudioCRM.Domain.Entities;
using StudioCRM.Infrastructure.Services;

namespace StudioCRM.Tests.IntegrationTests;

public class TrainerPortalSeriesTests
{
    // Trainer series: trainer forced to the logged-in one, own clients and locations only, no group/public series,
    // and deleting keeps past/completed occurrences.
    [PostgresFact]
    public Task TrainerCreatesOwnIndividualSeriesAndDeletesOnlyFutureSessions() => VerifySeriesAsync(false);

    [PostgresFact]
    public Task DemoTrainerCreatesSeriesWithoutOutlookAndDeletesOnlyFutureSessions() => VerifySeriesAsync(true);

    private static async Task VerifySeriesAsync(bool demo)
    {
        await using var database = await TestDatabase.CreateAsync();
        await using var legacy = database.Context();
        var organizationId = StudioCRM.Infrastructure.Persistence.StudioCRMDbContext.LegacyOrganizationId;
        if (demo)
        {
            var profile = new OrganizationBrandingProfile { Id = Guid.NewGuid(), Name = "Demo" };
            var org = new Organization { Id = Guid.NewGuid(), Name = "Demo", Slug = "series-demo", Status = "Active", BrandingProfileId = profile.Id };
            legacy.AddRange(profile, org); await legacy.SaveChangesAsync(); organizationId = org.Id;
        }
        await using var db = database.Context(organizationId: organizationId);
        var location = new Location { Name = "Series studio" };
        var foreignLocation = new Location { Name = "Foreign studio" };
        var trainer = new Trainer { User = new User { Email = "series-trainer@test.invalid", FirstName = "S", LastName = "Trener" } };
        var other = new Trainer { User = new User { Email = "series-other@test.invalid", FirstName = "O", LastName = "Trener" } };
        var client = new Client { FirstName = "Own", LastName = "Client", Email = "own-series@test.invalid", Trainer = trainer, Location = location };
        var foreignClient = new Client { FirstName = "Foreign", LastName = "Client", Email = "foreign-series@test.invalid", Trainer = other, Location = location };
        db.AddRange(client, foreignClient, foreignLocation,
            new TrainerLocation { Trainer = trainer, Location = location }, new TrainerLocation { Trainer = other, Location = location });
        await db.SaveChangesAsync();

        var actor = new PortalSecurityTests.Actor(trainer.UserId);
        var portal = new TrainerPortalService(db, actor, null!, PortalSecurityTests.Sessions(db, actor), null!);
        var start = DateTime.UtcNow.Date.AddDays(3).AddHours(8);
        CreateSessionSeriesDto Request(int locationId, int clientId, bool publicSession = false, string? type = null) => new()
        {
            Session = new CreateSessionDto
            {
                StartAt = start, EndAt = start.AddHours(1), TrainerId = other.Id, LocationId = locationId,
                IsPubliclyBookable = publicSession, PlannedSessionType = type,
                Participants = [new CreateSessionParticipantDto { ClientId = clientId, CountsAgainstPackage = true, SessionsCharged = 1 }]
            },
            Recurrence = new SessionRecurrenceDto { Frequency = "Weekly", Interval = 1, OccurrencesCount = 4 }
        };

        var series = await portal.CreateSessionSeriesAsync(Request(location.Id, client.Id));
        Assert.Equal(4, series.OccurrencesCount);
        if (demo)
        {
            Assert.False(series.OutlookSeriesSynced);
            Assert.Contains("not enabled", series.OutlookSyncWarning);
            Assert.Empty(await legacy.Sessions.ToListAsync());
        }
        Assert.All(await db.Sessions.Where(s => s.RecurringGroupId == series.RecurringGroupId).ToListAsync(),
            s => Assert.Equal(trainer.Id, s.TrainerId));

        await Assert.ThrowsAsync<InvalidOperationException>(() => portal.CreateSessionSeriesAsync(Request(location.Id, foreignClient.Id)));
        await Assert.ThrowsAsync<InvalidOperationException>(() => portal.CreateSessionSeriesAsync(Request(foreignLocation.Id, client.Id)));
        await Assert.ThrowsAsync<InvalidOperationException>(() => portal.CreateSessionSeriesAsync(Request(location.Id, client.Id, publicSession: true)));
        await Assert.ThrowsAsync<InvalidOperationException>(() => portal.CreateSessionSeriesAsync(Request(location.Id, client.Id, type: "Group")));

        var otherActor = new PortalSecurityTests.Actor(other.UserId);
        var otherPortal = new TrainerPortalService(db, otherActor, null!, PortalSecurityTests.Sessions(db, otherActor), null!);
        Assert.Null(await otherPortal.DeleteSessionSeriesAsync(series.RecurringGroupId));
        Assert.Null(await otherPortal.SyncSessionSeriesToOutlookAsync(series.RecurringGroupId, null));

        // First occurrence already happened and was completed: it must survive the trainer's delete.
        var first = await db.Sessions.OrderBy(s => s.StartAt).FirstAsync(s => s.RecurringGroupId == series.RecurringGroupId);
        first.StartAt = DateTime.UtcNow.AddDays(-1);
        first.EndAt = first.StartAt.AddHours(1);
        first.Status = "Completed";
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        var deleted = await portal.DeleteSessionSeriesAsync(series.RecurringGroupId);
        Assert.NotNull(deleted);
        Assert.Equal((3, 1), (deleted!.DeletedSessionsCount, deleted.KeptSessionsCount));
        Assert.Equal(first.Id, Assert.Single(await db.Sessions.Where(s => s.RecurringGroupId == series.RecurringGroupId).ToListAsync()).Id);
    }
}
