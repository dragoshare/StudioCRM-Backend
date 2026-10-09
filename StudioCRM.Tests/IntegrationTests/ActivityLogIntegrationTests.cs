using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using StudioCRM.Application.DTOs.ActivityLog;
using StudioCRM.Application.Interfaces;
using StudioCRM.Domain.Entities;
using StudioCRM.Infrastructure.Services;

namespace StudioCRM.Tests.IntegrationTests;

public class ActivityLogIntegrationTests
{
    [PostgresFact]
    public async Task BothSavePathsPreserveOfflineClientCalendarAddressGeneration()
    {
        // Saving now includes a real transaction: exercise the old interceptor-only
        // calendar-address test against PostgreSQL rather than suppressing SaveChanges.
        await using var database = await TestDatabase.CreateAsync();
        foreach (var asyncSave in new[] { false, true })
        {
            await using var db = database.Context(outlookSettings: new() { CalendarEmailDomain = "calendar.example.test" });
            var client = new Client { FirstName = "Anna", LastName = "Nowak", Location = new Location { Name = "Studio" } };
            db.Add(client);
            if (asyncSave) await db.SaveChangesAsync(); else db.SaveChanges();
            Assert.EndsWith("@calendar.example.test", client.CalendarEmail);
            Assert.Equal("", client.Email);
            Assert.Null(client.UserId);
            var audit = await db.ActivityLogEntries.SingleAsync(e => e.EntityType == "Client" && e.EntityId == client.Id.ToString());
            Assert.Contains(client.CalendarEmail!, audit.AfterJson!);
        }
    }

    [PostgresFact]
    public async Task MigrationCreatesReadableAuditTable()
    {
        await using var database = await TestDatabase.CreateAsync();
        await using var db = database.Context();
        await db.Database.ExecuteSqlRawAsync("DROP TABLE \"ActivityLogEntries\"");
        var migration = new StudioCRM.Infrastructure.Migrations.AddGlobalActivityLog();
        var commands = db.GetService<IMigrationsSqlGenerator>().Generate(migration.UpOperations, db.Model);
        foreach (var command in commands) await db.Database.ExecuteSqlRawAsync(command.CommandText);
        db.Add(new Package { Name = "After migration" });
        await db.SaveChangesAsync();
        Assert.Single(await db.ActivityLogEntries.ToListAsync());
    }

    [PostgresFact]
    public async Task RecordsRealKeysActorDiffsFiltersAndSurvivesDeletion()
    {
        await using var database = await TestDatabase.CreateAsync();
        await using var setup = database.Context();
        var owner = new User { Email = "owner@activity.test", FirstName = "Anna", LastName = "Owner" };
        setup.Add(owner);
        await setup.SaveChangesAsync();
        var actor = new Actor(owner.Id);
        await using var db = database.Context(actor);
        var package = new Package { Name = "Before", Price = 100 };
        db.Add(package);
        await db.SaveChangesAsync();
        var service = new ActivityLogService(db, actor);
        var filter = new ActivityLogFilter { EntityType = "Package", EntityId = package.Id.ToString(), ActorUserId = owner.Id };
        var created = Assert.Single((await service.GetAsync(filter)).Items);
        Assert.Equal("Created", created.Operation);
        Assert.Equal("Anna Owner", created.ActorName);
        Assert.Null(created.Before);
        Assert.Equal(package.Id, created.After!.Value.GetProperty("id").GetInt32());
        package.Price = 120;
        package.Name = "After";
        db.SaveChanges();
        var updated = (await service.GetAsync(filter)).Items[0];
        Assert.Equal("Updated", updated.Operation);
        Assert.Equal(100m, updated.Before!.Value.GetProperty("price").GetDecimal());
        Assert.Equal(120m, updated.After!.Value.GetProperty("price").GetDecimal());
        Assert.Equal(new[] { "name", "price" }, updated.ChangedFields.Order().ToArray());
        db.Remove(package);
        await db.SaveChangesAsync();
        var history = await service.GetAsync(filter);
        Assert.Equal(3, history.TotalCount);
        Assert.Null(history.Items[0].After);
        Assert.Equal("Deleted", history.Items[0].Operation);
        filter.PageSize = 1;
        filter.Page = 2;
        Assert.Equal("Updated", Assert.Single((await service.GetAsync(filter)).Items).Operation);
        filter.Page = 1;
        filter.Search = "after";
        Assert.Equal(2, (await service.GetAsync(filter)).TotalCount);
        filter.Operation = "Created";
        Assert.Empty((await service.GetAsync(filter)).Items);
        filter.Search = null;
        filter.From = created.CreatedAt;
        filter.To = created.CreatedAt.AddTicks(10);
        Assert.Single((await service.GetAsync(filter)).Items);
        filter.Source = "System";
        Assert.Empty((await service.GetAsync(filter)).Items);
    }

    [PostgresFact]
    public async Task BusinessChangeRollsBackWhenAuditInsertFailsIncludingInsideExistingTransaction()
    {
        await using var database = await TestDatabase.CreateAsync();
        await using var db = database.Context();
        await db.Database.ExecuteSqlRawAsync("ALTER TABLE \"ActivityLogEntries\" ADD CONSTRAINT reject_audit CHECK (false)");
        var package = new Package { Name = "Must not persist" };
        db.Add(package);
        await Assert.ThrowsAnyAsync<Exception>(() => db.SaveChangesAsync());
        db.ChangeTracker.Clear();
        Assert.Empty(await db.Packages.ToListAsync());
        await using var tx = await db.Database.BeginTransactionAsync();
        db.Add(new Package { Name = "Must not persist in outer transaction" });
        await Assert.ThrowsAnyAsync<Exception>(() => db.SaveChangesAsync());
        db.ChangeTracker.Clear();
        Assert.Empty(await db.Packages.ToListAsync());
        await tx.CommitAsync();
    }

    [PostgresFact]
    public async Task OuterRollbackRemovesHistoryAndFalseSaveRetainsTrackedState()
    {
        await using var database = await TestDatabase.CreateAsync();
        await using var db = database.Context();
        await using (var tx = await db.Database.BeginTransactionAsync())
        {
            var package = new Package { Name = "Temporary" };
            db.Add(package);
            await db.SaveChangesAsync(false);
            Assert.Equal(EntityState.Added, db.Entry(package).State);
            Assert.Single(await db.ActivityLogEntries.ToListAsync());
            await tx.RollbackAsync();
        }
        db.ChangeTracker.Clear();
        Assert.Empty(await db.ActivityLogEntries.ToListAsync());
        Assert.Empty(await db.Packages.ToListAsync());
    }

    [PostgresFact]
    public async Task GroupsRelatedChangesAndProtectsExistingHistory()
    {
        await using var database = await TestDatabase.CreateAsync();
        await using var db = database.Context();
        db.AddRange(new Package { Name = "A" }, new Package { Name = "B" });
        db.SaveChanges(false);
        db.ChangeTracker.AcceptAllChanges();
        var logs = await db.ActivityLogEntries.ToListAsync();
        Assert.Equal(2, logs.Count);
        Assert.Single(logs.Select(l => l.ChangeSetId).Distinct());
        Assert.All(logs, l => Assert.Equal("System", l.Source));
        db.Remove(logs[0]);
        await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());
    }

    [Fact]
    public async Task NonOwnerCannotReadAndInvalidFiltersAreRejectedBeforeQuerying()
    {
        using var db = new StudioCRM.Infrastructure.Persistence.StudioCRMDbContext(
            new DbContextOptionsBuilder<StudioCRM.Infrastructure.Persistence.StudioCRMDbContext>()
                .UseNpgsql("Host=localhost;Database=unused;Username=unused").Options);
        var denied = new ActivityLogService(db, new Actor(1, false));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => denied.GetAsync(new()));
        Assert.Throws<UnauthorizedAccessException>(() => denied.GetMetadata());
        var owner = new ActivityLogService(db, new Actor(1));
        foreach (var filter in new[] { new ActivityLogFilter { PageSize = 101 }, new() { EntityId = "1" }, new() { Operation = "Invalid" }, new() { From = DateTimeOffset.UtcNow, To = DateTimeOffset.UtcNow.AddDays(-1) } })
            await Assert.ThrowsAsync<InvalidOperationException>(() => owner.GetAsync(filter));
        var authorization = (Microsoft.AspNetCore.Authorization.AuthorizeAttribute)Attribute.GetCustomAttribute(
            typeof(StudioCRM.Api.Controllers.ActivityLogController), typeof(Microsoft.AspNetCore.Authorization.AuthorizeAttribute))!;
        Assert.Equal("Owner", authorization.Roles);
    }

    private sealed class Actor(int id, bool owner = true) : ICurrentUserService
    {
        public int? UserId => id;
        public string? Email => "owner@activity.test";
        public List<string> Roles => owner ? ["Owner"] : ["Trainer"];
        public bool IsAuthenticated => true;
        public bool IsOwner => owner;
        public bool IsTrainer => !owner;
        public bool IsClient => false;
    }
}
