using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using StudioCRM.Application.DTOs.ActivityLog;
using StudioCRM.Application.Interfaces;
using StudioCRM.Domain.Entities;
using StudioCRM.Infrastructure.Persistence;
using StudioCRM.Infrastructure.Services;

namespace StudioCRM.Tests.IntegrationTests;

public class ActivityLogLabelTests
{
    [PostgresFact]
    public async Task NamesAreBatchedFrozenAndHistoryReadsOnlyAuditTable()
    {
        await using var database = await TestDatabase.CreateAsync();
        await using var setup = database.Context();
        var client = new Client { FirstName = "Jan", LastName = "Kowalski", Location = new Location { Name = "Studio" } };
        setup.Add(client);
        await setup.SaveChangesAsync();
        var queries = new Queries();
        await using var db = new StudioCRMDbContext(new DbContextOptionsBuilder<StudioCRMDbContext>()
            .UseNpgsql(setup.Database.GetConnectionString()).AddInterceptors(queries).Options);
        db.AddRange(Enumerable.Range(0, 25).Select(_ => new ClientPayment { ClientId = client.Id, Amount = 10 }));
        await db.SaveChangesAsync();
        Assert.Single(queries.Commands.Where(sql => sql.Contains("FROM \"Clients\"")));
        Assert.Empty(db.ChangeTracker.Entries<Client>());
        client.FirstName = "Adam";
        await setup.SaveChangesAsync();

        queries.Commands.Clear();
        var history = await new ActivityLogService(db, new Actor(1)).GetAsync(new ActivityLogFilter { EntityType = "ClientPayment" });
        Assert.Equal(25, history.TotalCount);
        Assert.All(history.Items, row => Assert.Equal("Płatność — Jan Kowalski", row.EntityLabel));
        Assert.Equal(2, queries.Commands.Count);
        Assert.All(queries.Commands, sql =>
        {
            Assert.Contains("FROM \"ActivityLogEntries\"", sql);
            Assert.DoesNotContain("JOIN", sql);
        });

        // A new write uses the current name even with only a foreign key and sync SaveChanges.
        var balance = new ClientBalanceTransaction { ClientId = client.Id, Amount = 1 };
        db.Add(balance);
        db.SaveChanges();
        var balanceLabel = await db.ActivityLogEntries.SingleAsync(e => e.EntityType == "ClientBalanceTransaction");
        Assert.Equal("Zmiana salda — Adam Kowalski", balanceLabel.EntityLabel);

        client.IsDeleted = true;
        await setup.SaveChangesAsync();
        db.ChangeTracker.Clear();
        var payment = await db.ClientPayments.IgnoreQueryFilters().FirstAsync();
        db.Remove(payment);
        await db.SaveChangesAsync();
        var deletion = await db.ActivityLogEntries.SingleAsync(e => e.EntityType == "ClientPayment" && e.Operation == "Deleted");
        Assert.Equal("Płatność — Adam Kowalski", deletion.EntityLabel);
    }

    [PostgresFact]
    public async Task UnloadedTrainerContractAndLocationAreResolvedBeforeDeletion()
    {
        await using var database = await TestDatabase.CreateAsync();
        await using var setup = database.Context();
        var user = new User { FirstName = "Anna", LastName = "Trener" };
        var trainer = new Trainer { User = user };
        var location = new Location { Name = "Kłaj" };
        var contract = new TrainerContract { Trainer = trainer, ContractNumber = "UM/1" };
        setup.Add(new TrainerContractLocation { TrainerContract = contract, Location = location });
        await setup.SaveChangesAsync();
        await using var db = database.Context();
        var assignment = await db.TrainerContractLocations.SingleAsync();
        db.Remove(assignment);
        db.SaveChanges();
        var label = await db.ActivityLogEntries.SingleAsync(e => e.EntityType == "TrainerContractLocation" && e.Operation == "Deleted");
        Assert.Equal("Lokalizacja umowy trenera — Anna Trener — UM/1 — Kłaj", label.EntityLabel);

        // Same-save renames take precedence over database values, even when navigations were not included.
        var loadedUser = await db.Users.SingleAsync();
        loadedUser.FirstName = "Maria";
        db.Add(new TrainerRate { TrainerId = trainer.Id, Rate = 90 });
        await db.SaveChangesAsync();
        Assert.Equal("Stawka trenera — Maria Trener",
            (await db.ActivityLogEntries.SingleAsync(e => e.EntityType == "TrainerRate")).EntityLabel);
        Assert.Equal("Lokalizacja umowy trenera — Anna Trener — UM/1 — Kłaj", label.EntityLabel);
    }

    [PostgresFact]
    public async Task ActorFilterUsesUserIdEvenWhenNamesMatchOrChange()
    {
        await using var database = await TestDatabase.CreateAsync();
        await using var setup = database.Context();
        var first = new User { Email = "first@example.test", FirstName = "Jan", LastName = "Owner" };
        var second = new User { Email = "second@example.test", FirstName = "Jan", LastName = "Owner" };
        setup.AddRange(first, second);
        await setup.SaveChangesAsync();
        foreach (var actor in new[] { first, second })
        {
            await using var db = database.Context(new Actor(actor.Id));
            db.Add(new Package { Name = "Pakiet" });
            await db.SaveChangesAsync();
        }
        first.FirstName = "Adam";
        await setup.SaveChangesAsync();
        var service = new ActivityLogService(setup, new Actor(first.Id));
        var result = await service.GetAsync(new() { ActorUserId = first.Id, EntityType = "Package" });
        var entry = Assert.Single(result.Items);
        Assert.Equal(first.Id, entry.ActorUserId);
        Assert.Equal("Jan Owner", entry.ActorName);
        Assert.Single((await service.GetAsync(new() { ActorUserId = second.Id, EntityType = "Package" })).Items);
        Assert.Empty((await service.GetAsync(new() { Source = "System", EntityType = "Package" })).Items);
    }

    private sealed class Actor(int id) : ICurrentUserService
    {
        public int? UserId => id;
        public string? Email => null;
        public List<string> Roles => ["Owner"];
        public bool IsAuthenticated => true;
        public bool IsOwner => true;
        public bool IsTrainer => false;
        public bool IsClient => false;
    }

    private sealed class Queries : DbCommandInterceptor
    {
        public List<string> Commands { get; } = new();
        public override InterceptionResult<DbDataReader> ReaderExecuting(DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result)
        {
            Commands.Add(command.CommandText);
            return result;
        }
        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command, CommandEventData eventData,
            InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
        {
            Commands.Add(command.CommandText);
            return ValueTask.FromResult(result);
        }
    }
}
