using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using StudioCRM.Application.DTOs.Billing;
using StudioCRM.Application.DTOs.Clients;
using StudioCRM.Application.DTOs.Sessions;
using StudioCRM.Application.DTOs.TrainerSettlements;
using StudioCRM.Domain.Entities;
using StudioCRM.Domain.Enums;
using StudioCRM.Infrastructure.Services;

namespace StudioCRM.Tests.IntegrationTests;

// Server-side paging, filters and aggregates for the Owner panel (docs/BACKEND_FOR_FRONTEND_OPTIMIZATION.md).
public class OwnerPanelReadTests
{
    [PostgresFact]
    public async Task TrainerSettlementListMatchesSingleSettlementAndSummarizesWholeFilteredSet()
    {
        await using var database = await TestDatabase.CreateAsync();
        await using var db = database.Context();
        var start = new DateTime(2026, 10, 5, 10, 0, 0, DateTimeKind.Utc);
        var location = new Location { Name = "Main" };
        var other = new Location { Name = "Other" };
        Trainer Make(string last) => new() { User = new User { Email = $"{last}@settle.test", FirstName = "T", LastName = last } };
        var anna = Make("Anna"); var bart = Make("Bart"); var idle = Make("Idle");
        db.Add(new TrainerLocation { Trainer = idle, Location = other });
        foreach (var (trainer, hours) in new[] { (anna, 1), (bart, 2) })
        {
            db.Add(new TrainerLocation { Trainer = trainer, Location = location });
            db.Add(new TrainerContract
            {
                Trainer = trainer, ContractNumber = trainer.User.LastName, ContractType = TrainerContractType.Zlecenie,
                SignedAt = start.AddMonths(-1), ValidFrom = start.AddMonths(-1),
                ContractLocations = [new TrainerContractLocation { Location = location }]
            });
            db.Add(new TrainerRate { Trainer = trainer, SessionType = "Hourly", Rate = 100, ValidFrom = start.AddMonths(-1) });
            db.Add(new Session { Trainer = trainer, Location = location, StartAt = start, EndAt = start.AddHours(hours), Status = "Completed", ActualSessionType = "OneToOne" });
        }
        db.Add(new TrainerMonthlySettlement { Trainer = bart, Year = 2026, Month = 10, IsPaid = true, PaidAt = start });
        await db.SaveChangesAsync();

        var service = new TrainerSettlementService(db, new PortalSecurityTests.Actor(1, "Owner"));
        var page = await service.GetMonthlySettlementsAsync(new TrainerSettlementListFilter { Year = 2026, Month = 10, LocationId = location.Id, PageSize = 1 });
        Assert.Equal((2, 2, 1), (page.TotalCount, page.TotalPages, page.Items.Count));
        Assert.Equal("T Anna", page.Items[0].TrainerFullName);
        Assert.Equal((300m, 200m, 100m), (page.Summary.TotalAmount, page.Summary.PaidAmount, page.Summary.UnpaidAmount));
        Assert.Equal((3m, 2, "PLN"), (page.Summary.TotalHours, page.Summary.TotalSessions, page.Currency));
        var single = (await service.GetMonthlySettlementAsync(anna.Id, 2026, 10, location.Id))!;
        Assert.Equal((single.TotalAmount, single.TotalHours, single.TotalSessions, single.IsPaid),
            (page.Items[0].TotalAmount, page.Items[0].TotalHours, page.Items[0].TotalSessions, page.Items[0].IsPaid));

        var all = await service.GetMonthlySettlementsAsync(new TrainerSettlementListFilter { Year = 2026, Month = 10 });
        Assert.Equal(3, all.TotalCount);
        Assert.Equal(0m, all.Items.Single(i => i.TrainerId == idle.Id).TotalAmount);
        Assert.Equal(bart.Id, Assert.Single((await service.GetMonthlySettlementsAsync(new TrainerSettlementListFilter { Year = 2026, Month = 10, Search = "bart" })).Items).TrainerId);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            new TrainerSettlementService(db, new PortalSecurityTests.Actor(1)).GetMonthlySettlementsAsync(new TrainerSettlementListFilter { Year = 2026, Month = 10 }));
    }

    [PostgresFact]
    public async Task ClientPageFiltersBeforePagingAndSummarizesFilteredSet()
    {
        await using var database = await TestDatabase.CreateAsync();
        await using var db = database.Context();
        var location = new Location { Name = "Main" };
        var trainer = new Trainer { User = new User { Email = "page-trainer@test.invalid", FirstName = "T", LastName = "One" } };
        Client Make(string last, string status = "Active") => new() { FirstName = "K", LastName = last, Location = location, Trainer = trainer, Status = status };
        var withPackage = Make("Alpha"); var noPackage = Make("Beta", "Inactive"); var archived = Make("Gamma");
        archived.IsDeleted = true; archived.DeletedAt = DateTime.UtcNow;
        db.AddRange(withPackage, noPackage, archived);
        db.Add(new ClientPackage { Client = withPackage, Package = new Package { Name = "P" }, Name = "P", TotalSessions = 4, PurchaseDate = DateTime.UtcNow, Location = location });
        await db.SaveChangesAsync();

        var service = new ClientService(db, new PortalSecurityTests.Actor(1, "Owner"), null!, null!);
        var page = await service.GetPageAsync(new ClientPageFilter { Sort = "name", PageSize = 1 });
        Assert.Equal((2, 2), (page.TotalCount, page.TotalPages));
        Assert.Equal("Alpha", Assert.Single(page.Items).LastName);
        Assert.Equal((1, 1), (page.Summary.ActiveClientsCount, page.Summary.WithActivePackageCount));
        Assert.NotEmpty(page.Items[0].ActivePackages);

        Assert.Equal("Beta", Assert.Single((await service.GetPageAsync(new ClientPageFilter { PackageStatus = "none" })).Items).LastName);
        Assert.Equal("Gamma", Assert.Single((await service.GetPageAsync(new ClientPageFilter { IsArchived = true })).Items).LastName);
        Assert.Equal(["Beta", "Alpha"], (await service.GetPageAsync(new ClientPageFilter { Sort = "name", Order = "desc" })).Items.Select(c => c.LastName));

        var options = await service.GetFilterOptionsAsync(new OwnerFilterOptionsQuery { Search = "alp", Limit = 5 });
        Assert.Equal(withPackage.Id, Assert.Single(options.Clients).Id);
        Assert.Empty(options.Trainers);
        Assert.Equal(trainer.Id, Assert.Single((await service.GetFilterOptionsAsync(new OwnerFilterOptionsQuery { Search = "one" })).Trainers).Id);
    }

    [PostgresFact]
    public async Task SessionHistoryPagesNewestFirstWhileScheduleListStaysAscending()
    {
        await using var database = await TestDatabase.CreateAsync();
        await using var db = database.Context();
        var trainer = new Trainer { User = new User { Email = "history@test.invalid" } };
        var location = new Location { Name = "Main" };
        var start = new DateTime(2026, 9, 1, 10, 0, 0, DateTimeKind.Utc);
        var sessions = Enumerable.Range(0, 3).Select(i => new Session
        {
            Title = $"S{i}", Trainer = trainer, Location = location, StartAt = start.AddDays(i), EndAt = start.AddDays(i).AddHours(1), Status = "Completed"
        }).ToList();
        db.AddRange(sessions); await db.SaveChangesAsync();
        var service = new SessionService(db, new PortalSecurityTests.Actor(1, "Owner"), null!, null!, null!, null!, NullLogger<SessionService>.Instance);

        var schedule = await service.GetFilteredAsync(new SessionFilterDto { TrainerId = trainer.Id });
        Assert.Equal(sessions.Select(s => s.Id), schedule.Select(s => s.Id));
        var history = await service.GetFilteredPageAsync(new SessionFilterDto { TrainerId = trainer.Id, Page = 1, PageSize = 2 });
        Assert.Equal((3, 2), (history.TotalCount, history.TotalPages));
        Assert.Equal([sessions[2].Id, sessions[1].Id], history.Items.Select(s => s.Id));
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.GetFilteredPageAsync(new SessionFilterDto { PageSize = 101 }));
    }

    [PostgresFact]
    public async Task PaymentListCarriesPackageStateAndFiltersByEffectiveLocation()
    {
        await using var database = await TestDatabase.CreateAsync();
        await using var db = database.Context();
        var owner = new User { Email = "eff-owner@test.invalid" };
        var home = new Location { Name = "Home" };
        var client = new Client { FirstName = "Pay", LastName = "Er", Location = home };
        var package = new ClientPackage { Client = client, Package = new Package { Name = "P" }, Name = "P", TotalPrice = 500, AmountPaid = 200,
            PaymentStatus = PaymentStatus.PartiallyPaid, TotalSessions = 4, PurchaseDate = DateTime.UtcNow, Location = home };
        var legacy = new ClientPayment { Client = client, ClientPackage = package, Amount = 250, AppliedToPackageAmount = 200, BalanceCreditAmount = 50,
            Method = PaymentMethod.Cash, Status = ClientPaymentStatus.Confirmed, Source = ClientPaymentSource.StaffEntry, PaymentDate = DateTime.UtcNow };
        db.AddRange(owner, legacy); await db.SaveChangesAsync();
        var billing = PortalSecurityTests.Payments(db, new PortalSecurityTests.Actor(owner.Id, "Owner"));

        Assert.Empty((await billing.GetPaymentsAsync(new ClientPaymentFilterDto { LocationId = home.Id })).Items);
        var item = Assert.Single((await billing.GetPaymentsAsync(new ClientPaymentFilterDto { LocationId = home.Id, LocationMatch = "effective", Overpayment = true })).Items);
        Assert.Equal((home.Id, "Home", "ClientHome"), (item.EffectiveLocationId, item.EffectiveLocationName, item.EffectiveLocationSource));
        Assert.Equal((50m, true), (item.OverpaymentAmount, item.HasOverpayment));
        Assert.Equal((500m, 200m, 300m, "PartiallyPaid"), (item.PackageTotalPrice, item.PackageAmountPaid, item.PackageAmountDue, item.PackagePaymentStatus));
        Assert.Empty((await billing.GetPaymentsAsync(new ClientPaymentFilterDto { Overpayment = false })).Items);
    }

    [PostgresFact]
    public async Task IdempotencyKeyIsBoundToUserNotClientAndOperationCanBeLookedUp()
    {
        await using var database = await TestDatabase.CreateAsync();
        await using var db = database.Context();
        var user = new User { Email = "idem-owner@test.invalid" };
        var location = new Location { Name = "A", City = "Test" };
        var first = new Client { FirstName = "First", Location = location };
        var second = new Client { FirstName = "Second", Location = location };
        db.AddRange(user, first, second); await db.SaveChangesAsync();
        var service = PortalSecurityTests.Payments(db, new PortalSecurityTests.Actor(user.Id, "Owner"));
        var key = Guid.NewGuid();
        Assert.Null(await service.GetStaffPaymentOperationAsync(key));

        var created = await service.CreatePaymentAsStaffAsync(new() { ClientId = first.Id, Amount = 50, Method = PaymentMethod.Cash, IdempotencyKey = key });
        db.ChangeTracker.Clear();
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() =>
            service.CreatePaymentAsStaffAsync(new() { ClientId = second.Id, Amount = 50, Method = PaymentMethod.Cash, IdempotencyKey = key }));
        db.ChangeTracker.Clear();
        var status = (await service.GetStaffPaymentOperationAsync(key))!;
        Assert.Equal(("Completed", created.Id), (status.State, status.Result!.Id));
        Assert.Null(await PortalSecurityTests.Payments(db, new PortalSecurityTests.Actor(user.Id + 1000, "Owner")).GetStaffPaymentOperationAsync(key));
        Assert.Equal(1, await db.ClientPayments.CountAsync());
    }

    [PostgresFact]
    public async Task ClientPageSortsByUsageBalanceTrainerAndArchiveAndSearchesPhone()
    {
        await using var database = await TestDatabase.CreateAsync();
        await using var db = database.Context();
        var location = new Location { Name = "Main" };
        var zed = new Trainer { User = new User { Email = "zed@test.invalid", FirstName = "A", LastName = "Zed" } };
        var abe = new Trainer { User = new User { Email = "abe@test.invalid", FirstName = "A", LastName = "Abe" } };
        Client Make(string last, Trainer? trainer) => new() { FirstName = "K", LastName = last, Location = location, Trainer = trainer };
        var half = Make("Half", zed); var full = Make("Full", abe); var period = Make("Period", null); var none = Make("None", abe);
        half.PhoneNumber = "+48 600-100-200";
        ClientPackage Cycle(Client c, int total, int used, PackageBillingMode mode = PackageBillingMode.SessionBased) => new()
        {
            Client = c, Package = new Package { Name = "P" }, Name = "P", BillingMode = mode, TotalSessions = total, UsedSessions = used,
            PurchaseDate = DateTime.UtcNow, Location = location
        };
        db.AddRange(Cycle(half, 10, 5), Cycle(full, 4, 4), Cycle(period, 0, 0, PackageBillingMode.PeriodBased));
        db.Add(none);
        db.AddRange(
            new ClientBalanceTransaction { Client = half, Type = BalanceTransactionType.ManualAdjustment, Amount = 40 },
            new ClientBalanceTransaction { Client = full, Type = BalanceTransactionType.ManualAdjustment, Amount = -20 });
        await db.SaveChangesAsync();
        var service = new ClientService(db, new PortalSecurityTests.Actor(1, "Owner"), null!, null!);
        async Task<List<string>> Names(string sort) =>
            (await service.GetPageAsync(new ClientPageFilter { Sort = sort, PageSize = 100 })).Items.Select(c => c.LastName).ToList();

        // Clients without a personal/semi session-based package (period-only or none) go last in both directions.
        var noPersonal = new[] { period, none }.OrderBy(c => c.Id).Select(c => c.LastName).ToList();
        Assert.Equal(["Half", "Full", .. noPersonal], await Names("packageUsage"));
        Assert.Equal(["Full", "Half", .. noPersonal], await Names("packageUsageDesc"));
        Assert.Equal("Full", (await Names("balance"))[0]);
        Assert.Equal("Half", (await Names("balanceDesc"))[0]);
        Assert.Equal("Period", (await Names("trainer"))[^1]);
        // Trainer Abe (Full, None by client name) before Zed (Half); clients without a trainer last.
        Assert.Equal(["Full", "None", "Half"], (await Names("trainer")).Take(3));

        foreach (var search in new[] { "600100200", "+48600100200", "600 100" })
            Assert.Equal("Half", Assert.Single((await service.GetPageAsync(new ClientPageFilter { Search = search })).Items).LastName);

        none.IsDeleted = true; none.DeletedAt = DateTime.UtcNow.AddDays(-1);
        full.IsDeleted = true; full.DeletedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        var archived = await service.GetPageAsync(new ClientPageFilter { IsArchived = true, Sort = "archivedAtDesc" });
        Assert.Equal(["Full", "None"], archived.Items.Select(c => c.LastName));
        var pastEnd = await service.GetPageAsync(new ClientPageFilter { Page = 5, PageSize = 1 });
        Assert.Empty(pastEnd.Items);
        Assert.Equal((2, 2), (pastEnd.TotalCount, pastEnd.TotalPages));
    }

    [PostgresFact]
    public async Task FilterOptionsApplyExactStatusBeforeLimitAndKeepSelectedPerson()
    {
        await using var database = await TestDatabase.CreateAsync();
        await using var db = database.Context();
        var location = new Location { Name = "Main" };
        var inactive = Enumerable.Range(0, 3).Select(i => new Client { FirstName = "A", LastName = $"Inactive{i}", Location = location, Status = "Inactive" }).ToList();
        var active = new Client { FirstName = "Z", LastName = "Zactive", Location = location, Status = "Active" };
        var archived = new Client { FirstName = "Old", LastName = "Archived", Location = location, IsDeleted = true };
        var group = new Client { FirstName = "G", LastName = "Group", Location = new Location { Name = "Home" } };
        db.AddRange(inactive); db.AddRange(active, archived, group);
        db.Add(new ClientLocationMembership { Client = group, Location = location, GroupAccessEnabled = true });
        await db.SaveChangesAsync();
        var service = new ClientService(db, new PortalSecurityTests.Actor(1, "Owner"), null!, null!);

        Assert.Equal(active.Id, Assert.Single((await service.GetFilterOptionsAsync(new OwnerFilterOptionsQuery { ClientStatus = "Active", Limit = 1 })).Clients).Id);
        var withSelected = await service.GetFilterOptionsAsync(new OwnerFilterOptionsQuery { Limit = 1, SelectedClientId = archived.Id });
        Assert.Equal(2, withSelected.Clients.Count);
        Assert.True(withSelected.Clients.Single(c => c.Id == archived.Id) is { IsSelected: true, IsArchived: true });
        Assert.Equal(group.Id, Assert.Single((await service.GetFilterOptionsAsync(new OwnerFilterOptionsQuery { LocationId = location.Id, GroupAccess = true })).Clients).Id);
    }

    [PostgresFact]
    public async Task GroupEnrollmentDetailsCarryActionsCurrencyAndPendingPayments()
    {
        await using var database = await TestDatabase.CreateAsync();
        await using var db = database.Context();
        var location = new Location { Name = "Group" };
        var trainer = new Trainer { User = new User { Email = "ge@test.invalid" } };
        var client = new Client { FirstName = "Anna", Location = location, Trainer = trainer };
        var session = new Session { Title = "Group", Location = location, Trainer = trainer, PlannedSessionType = "Group",
            StartAt = DateTime.UtcNow.AddDays(2), EndAt = DateTime.UtcNow.AddDays(2).AddHours(1) };
        var package = new ClientPackage { Client = client, Package = new Package { Name = "G", BillingType = SessionBillingType.Group }, Name = "G",
            TotalPrice = 120, TotalSessions = 4, ExpectedBillingType = SessionBillingType.Group, PaymentStatus = PaymentStatus.Unpaid,
            IsActive = false, Location = location, Currency = "PLN" };
        var enrollment = new GroupEnrollmentRequest { Session = session, Client = client, ClientPackage = package };
        db.AddRange(enrollment, new ClientPayment { Client = client, ClientPackage = package, Amount = 50, Method = PaymentMethod.Blik,
            Status = ClientPaymentStatus.PendingConfirmation, Source = ClientPaymentSource.ClientRequest, PaymentDate = DateTime.UtcNow });
        await db.SaveChangesAsync();

        var owner = new GroupEnrollmentService(db, new PortalSecurityTests.Actor(trainer.UserId, "Owner"), null!);
        var details = await owner.GetAsync(enrollment.Id);
        Assert.Equal(("AwaitingPayment", "PLN", 50m), (details.Status, details.Currency, details.PendingPaymentsAmount));
        Assert.Equal(["RecordPayment"], details.AllowedActions);
        Assert.Equal(PackageBillingMode.SessionBased, details.PackageBillingMode);
        Assert.Equal(details.AllowedActions, Assert.Single((await owner.ListAsync(session.Id, 1, 25)).Items).AllowedActions);
    }

    [PostgresFact]
    public async Task OwnerPanelReadsDoNotLeakAnotherOrganizationsRows()
    {
        await using var database = await TestDatabase.CreateAsync();
        Guid other;
        await using (var seed = database.Context())
        {
            var profile = new OrganizationBrandingProfile { Id = Guid.NewGuid(), Name = "Other" };
            var organization = new Organization { Id = Guid.NewGuid(), Name = "Other", Slug = "other", BrandingProfileId = profile.Id };
            seed.AddRange(profile, organization); await seed.SaveChangesAsync();
            other = organization.Id;
        }
        await using (var foreign = database.Context(organizationId: other))
        {
            var trainer = new Trainer { User = new User { Email = "foreign-trainer@test.invalid", FirstName = "F", LastName = "Foreign" } };
            var client = new Client { FirstName = "F", LastName = "Foreign", Location = new Location { Name = "Foreign" }, Trainer = trainer };
            foreign.AddRange(client, new ClientPayment { Client = client, Amount = 10, Method = PaymentMethod.Cash, Status = ClientPaymentStatus.Confirmed,
                Source = ClientPaymentSource.StaffEntry, PaymentDate = DateTime.UtcNow });
            await foreign.SaveChangesAsync();
        }
        await using var db = database.Context();
        var owner = new PortalSecurityTests.Actor(1, "Owner");
        Assert.Equal(0, (await new ClientService(db, owner, null!, null!).GetPageAsync(new ClientPageFilter())).TotalCount);
        Assert.Equal(0, (await new ClientService(db, owner, null!, null!).GetPageAsync(new ClientPageFilter { IsArchived = true })).TotalCount);
        var options = await new ClientService(db, owner, null!, null!).GetFilterOptionsAsync(new OwnerFilterOptionsQuery { Search = "foreign" });
        Assert.Empty(options.Clients); Assert.Empty(options.Trainers);
        Assert.Equal(0, (await PortalSecurityTests.Payments(db, owner).GetPaymentsAsync(new ClientPaymentFilterDto())).TotalCount);
        Assert.Equal(0, (await new TrainerSettlementService(db, owner).GetMonthlySettlementsAsync(new TrainerSettlementListFilter { Year = 2026, Month = 10 })).TotalCount);
    }
}
