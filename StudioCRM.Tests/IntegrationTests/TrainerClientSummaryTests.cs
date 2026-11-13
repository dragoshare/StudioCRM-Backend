using Microsoft.EntityFrameworkCore;
using StudioCRM.Application.DTOs.TrainerPortal;
using StudioCRM.Domain.Entities;
using StudioCRM.Domain.Enums;
using StudioCRM.Infrastructure.Services;

namespace StudioCRM.Tests.IntegrationTests;

public class TrainerClientSummaryTests
{
    [PostgresFact]
    public async Task ListAndPageReuseOwnerSummaryWithoutExposingOtherTrainersClients()
    {
        await using var database = await TestDatabase.CreateAsync();
        await using var db = database.Context();
        var location = new Location { Name = "Studio" };
        var trainer = new Trainer { User = new User { Email = "summary-trainer@test.invalid", FirstName = "Jan", LastName = "Trener" } };
        var other = new Trainer { User = new User { Email = "other-summary@test.invalid" } };
        var client = new Client { FirstName = "Anna", LastName = "Test", Trainer = trainer, Location = location };
        var foreign = new Client { FirstName = "Hidden", Trainer = other, Location = location };
        var unassigned = new Client { FirstName = "Unassigned", Location = location };
        var deleted = new Client { FirstName = "Deleted", Trainer = trainer, Location = location, IsDeleted = true };
        var personal = new ClientPackage { Client = client, Package = new Package { Name = "Semi" }, Name = "Semi snapshot",
            TotalSessions = 8, UsedSessions = 3, PaymentStatus = PaymentStatus.Paid, Location = location };
        var period = new ClientPackage { Client = client, Package = new Package { Name = "Diet", BillingMode = PackageBillingMode.PeriodBased },
            Name = "Diet snapshot", BillingMode = PackageBillingMode.PeriodBased, TotalSessions = 0,
            PeriodStartsAt = DateTime.UtcNow.AddDays(-2), ValidUntil = DateTime.UtcNow.AddDays(28), PeriodDurationDays = 30, Location = location };
        var group = new ClientPackage { Client = client, Package = new Package { Name = "Group" }, Name = "Group snapshot",
            ExpectedBillingType = SessionBillingType.Group, TotalSessions = 4, UsedSessions = 5, Location = location };
        db.AddRange(personal, period, group, foreign, unassigned, deleted);
        db.Add(new ClientPackage { Client = client, Package = new Package { Name = "Closed" }, IsActive = false, TotalSessions = 9 });
        db.AddRange(new ClientBalanceTransaction { Client = client, Type = BalanceTransactionType.PackageAdjustment, Amount = 30 },
            new ClientBalanceTransaction { Client = client, Type = BalanceTransactionType.ManualAdjustment, Amount = -5 },
            new ClientBalanceTransaction { Client = client, Type = BalanceTransactionType.PaymentCredit, Amount = 500 },
            new ClientBalanceTransaction { Client = client, Type = BalanceTransactionType.PaymentReversal, Amount = -100 },
            new ClientBalanceTransaction { Client = foreign, Type = BalanceTransactionType.ManualAdjustment, Amount = 9000 });
        await db.SaveChangesAsync();
        var actor = new PortalSecurityTests.Actor(trainer.UserId);
        var clients = new ClientService(db, actor, null!, null!);
        var portal = new TrainerPortalService(db, actor, clients, null!, null!);
        var row = Assert.Single(await portal.GetClientsAsync());
        Assert.Equal(client.Id, row.ClientId); Assert.Equal(trainer.Id, row.TrainerId); Assert.Equal("Jan Trener", row.TrainerFullName);
        Assert.Equal(25, row.CurrentBalance); Assert.Equal(personal.Id, row.ActiveClientPackageId);
        Assert.Equal(8, row.ActivePackageTotalSessions); Assert.Equal(3, row.ActivePackageUsedSessions); Assert.Equal(5, row.ActivePackageRemainingSessions);
        Assert.Equal("Paid", row.ActivePackagePaymentStatus); Assert.Equal(3, row.ActivePackages.Count);
        var diet = Assert.Single(row.ActivePackages.Where(p => p.BillingMode == PackageBillingMode.PeriodBased));
        Assert.Equal(period.Id, diet.ClientPackageId); Assert.Equal("Active", diet.Status);
        Assert.NotNull(diet.ValidUntil); Assert.Null(diet.TotalSessions); Assert.Null(diet.UsedSessions); Assert.Null(diet.RemainingSessions);
        Assert.Equal(0, row.ActivePackages.Single(p => p.ClientPackageId == group.Id).RemainingSessions);
        var ownerRow = (await new ClientService(db, new PortalSecurityTests.Actor(999, "Owner"), null!, null!).GetAllAsync()).Single(c => c.Id == client.Id);
        Assert.Equal(ownerRow.CurrentBalance, row.CurrentBalance);
        Assert.Equal(ownerRow.ActiveClientPackageId, row.ActiveClientPackageId);
        Assert.Equal(ownerRow.ActivePackages.Select(p => p.ClientPackageId), row.ActivePackages.Select(p => p.ClientPackageId));
        var page = await portal.GetClientsPageAsync(new PortalPageFilter { Page = 1, PageSize = 1, Search = "Anna" });
        Assert.Equal(1, page.TotalCount); Assert.Equal(25, Assert.Single(page.Items).CurrentBalance);
        Assert.Empty((await portal.GetClientsPageAsync(new PortalPageFilter { Search = "Hidden" })).Items);
        var missing = new TrainerPortalService(db, new PortalSecurityTests.Actor(int.MaxValue), clients, null!, null!);
        Assert.Empty(await missing.GetClientsAsync());
    }

    [PostgresFact]
    public async Task PageShowsOwnerAmountDueAndSortsAndFiltersByIt()
    {
        await using var database = await TestDatabase.CreateAsync();
        await using var db = database.Context();
        var location = new Location { Name = "Studio" };
        var trainer = new Trainer { User = new User { Email = "due-trainer@test.invalid" } };
        var big = new Client { FirstName = "A", LastName = "Big", Trainer = trainer, Location = location };
        var small = new Client { FirstName = "B", LastName = "Small", Trainer = trainer, Location = location };
        var paid = new Client { FirstName = "C", LastName = "Paid", Trainer = trainer, Location = location };
        ClientPackage Bought(Client client, decimal total, decimal amountPaid, bool active = true) => new()
        {
            Client = client, Package = new Package { Name = "P" }, Name = "P", TotalSessions = 4,
            TotalPrice = total, AmountPaid = amountPaid, IsActive = active, Location = location
        };
        // Overpaid packages do not offset debts; closed (inactive) packages still count, as on the Owner list.
        db.AddRange(Bought(big, 500, 100), Bought(big, 300, 0, active: false), Bought(small, 200, 150),
            Bought(small, 100, 160, active: false), Bought(paid, 400, 400));
        await db.SaveChangesAsync();
        var actor = new PortalSecurityTests.Actor(trainer.UserId);
        var portal = new TrainerPortalService(db, actor, new ClientService(db, actor, null!, null!), null!, null!);

        var asc = await portal.GetClientsPageAsync(new PortalPageFilter { Sort = "amountDue" });
        Assert.Equal([paid.Id, small.Id, big.Id], asc.Items.Select(c => c.ClientId));
        Assert.Equal([0m, 50m, 700m], asc.Items.Select(c => c.TotalAmountDue));
        Assert.All(asc.Items, c => Assert.Equal("PLN", c.AmountDueCurrency));
        var desc = await portal.GetClientsPageAsync(new PortalPageFilter { Sort = "amountDueDesc", PageSize = 1 });
        Assert.Equal(3, desc.TotalCount); Assert.Equal(big.Id, Assert.Single(desc.Items).ClientId);
        var due = await portal.GetClientsPageAsync(new PortalPageFilter { HasAmountDue = true });
        Assert.Equal([big.Id, small.Id], due.Items.Select(c => c.ClientId));

        var owner = await new ClientService(db, new PortalSecurityTests.Actor(999, "Owner"), null!, null!)
            .GetPageAsync(new() { Sort = "amountDue", PageSize = 10 });
        Assert.Equal(owner.Items.Select(c => (c.Id, c.TotalAmountDue)), asc.Items.Select(c => (c.ClientId, c.TotalAmountDue)));
        Assert.Throws<System.ComponentModel.DataAnnotations.ValidationException>(() => new PortalPageFilter { Sort = "trainer" }.Validate());
    }

    [PostgresFact]
    public async Task TrainerCreatesClientRecordAssignedToThemselvesOnlyAtOwnLocation()
    {
        await using var database = await TestDatabase.CreateAsync();
        await using var db = database.Context();
        var location = new Location { Name = "Studio" };
        var foreignLocation = new Location { Name = "Elsewhere" };
        var trainer = new Trainer { User = new User { Email = "create-trainer@test.invalid", IsActive = true } };
        var other = new Trainer { User = new User { Email = "create-other@test.invalid", IsActive = true } };
        db.AddRange(new TrainerLocation { Trainer = trainer, Location = location },
            new TrainerLocation { Trainer = other, Location = foreignLocation });
        await db.SaveChangesAsync();
        var actor = new PortalSecurityTests.Actor(trainer.UserId);
        var portal = new TrainerPortalService(db, actor, new ClientService(db, actor, null!, null!), null!, null!);

        var created = await portal.CreateClientAsync(new() { FirstName = "Nowa", LastName = "Klientka", LocationId = location.Id });
        Assert.Equal(trainer.Id, created.TrainerId);
        Assert.Equal(created.Id, Assert.Single(await portal.GetClientsAsync()).ClientId);
        Assert.True(await db.ClientLocationMemberships.AnyAsync(m => m.ClientId == created.Id && m.LocationId == location.Id && m.IsHomeLocation));

        await Assert.ThrowsAsync<InvalidOperationException>(() => portal.CreateClientAsync(
            new() { FirstName = "Cudza", LastName = "Klientka", LocationId = location.Id, TrainerId = other.Id }));
        await Assert.ThrowsAsync<InvalidOperationException>(() => portal.CreateClientAsync(
            new() { FirstName = "Inna", LastName = "Lokalizacja", LocationId = foreignLocation.Id }));
        Assert.Equal(1, await db.Clients.CountAsync());
    }

    [PostgresFact]
    public async Task PeriodOnlyClientKeepsLegacyEmptyPackageFieldsAndExposesScheduledService()
    {
        await using var database = await TestDatabase.CreateAsync(); await using var db = database.Context();
        var trainer = new Trainer { User = new User { Email = "period-summary@test.invalid" } };
        var client = new Client { FirstName = "Remote", Trainer = trainer, Location = new Location { Name = "Remote" } };
        db.Add(new ClientPackage { Client = client, Package = new Package { Name = "Remote", BillingMode = PackageBillingMode.PeriodBased },
            BillingMode = PackageBillingMode.PeriodBased, Name = "Remote", TotalSessions = 0, PeriodDurationDays = 30,
            PeriodStartsAt = DateTime.UtcNow.AddDays(2), ValidUntil = DateTime.UtcNow.AddDays(32) });
        await db.SaveChangesAsync();
        var portal = new TrainerPortalService(db, new PortalSecurityTests.Actor(trainer.UserId), null!, null!, null!);
        var row = Assert.Single(await portal.GetClientsAsync());
        Assert.Null(row.ActiveClientPackageId); Assert.Equal(0, row.ActivePackageTotalSessions); Assert.Equal(0, row.CurrentBalance);
        Assert.Equal("Scheduled", Assert.Single(row.ActivePackages).Status);
    }
}
