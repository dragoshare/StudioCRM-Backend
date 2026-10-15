using Microsoft.Extensions.Options;
using StudioCRM.Application.DTOs.Billing;
using StudioCRM.Application.DTOs.TrainerPortal;
using StudioCRM.Application.Settings;
using StudioCRM.Domain.Entities;
using StudioCRM.Domain.Enums;
using StudioCRM.Infrastructure.Services;

namespace StudioCRM.Tests.IntegrationTests;

public class ListFilterQueryTests
{
    [PostgresFact]
    public async Task TrainerClientPageFiltersByActivePackageAndSortsByBalanceAndUsage()
    {
        await using var database = await TestDatabase.CreateAsync();
        await using var db = database.Context();
        var location = new Location { Name = "Studio" };
        var trainer = new Trainer { User = new User { Email = "list-trainer@test.invalid" } };
        var other = new Trainer { User = new User { Email = "list-other@test.invalid" } };
        Client Make(string last) => new() { FirstName = "K", LastName = last, Trainer = trainer, Location = location };
        var half = Make("Half"); var full = Make("Full"); var groupOnly = Make("GroupOnly"); var none = Make("None"); var tie = Make("Tie");
        var foreign = new Client { FirstName = "K", LastName = "Foreign", Trainer = other, Location = location };
        ClientPackage Cycle(Client c, int total, int used, SessionBillingType type = SessionBillingType.OneToOne) => new()
        {
            Client = c, Package = new Package { Name = "P" }, Name = "P", ExpectedBillingType = type,
            TotalSessions = total, UsedSessions = used, PurchaseDate = DateTime.UtcNow, Location = location
        };
        db.AddRange(Cycle(half, 10, 5), Cycle(full, 4, 4), Cycle(groupOnly, 8, 1, SessionBillingType.Group), Cycle(tie, 8, 4), Cycle(foreign, 10, 0));
        db.Add(new ClientPackage { Client = none, Package = new Package { Name = "Old" }, Name = "Old", IsActive = false, TotalSessions = 5, UsedSessions = 5 });
        db.AddRange(
            new ClientBalanceTransaction { Client = half, Type = BalanceTransactionType.ManualAdjustment, Amount = 40 },
            new ClientBalanceTransaction { Client = full, Type = BalanceTransactionType.ManualAdjustment, Amount = -20 },
            new ClientBalanceTransaction { Client = none, Type = BalanceTransactionType.PaymentCredit, Amount = 1000 },
            new ClientBalanceTransaction { Client = foreign, Type = BalanceTransactionType.ManualAdjustment, Amount = 9999 });
        await db.SaveChangesAsync();
        var portal = new TrainerPortalService(db, new PortalSecurityTests.Actor(trainer.UserId), null!, null!, null!);
        async Task<List<string>> Names(string sort, bool? hasActive = null) =>
            (await portal.GetClientsPageAsync(new PortalPageFilter { Sort = sort, HasActivePackage = hasActive })).Items
                .Select(c => c.FullName.Split(' ')[1]).ToList();

        var withPackage = await portal.GetClientsPageAsync(new PortalPageFilter { HasActivePackage = true });
        Assert.Equal(4, withPackage.TotalCount);
        Assert.Equal(["Full", "GroupOnly", "Half", "Tie"], withPackage.Items.Select(c => c.FullName.Split(' ')[1]));
        Assert.Equal(["None"], await Names("name", false));

        IEnumerable<string> ById(params Client[] clients) => clients.OrderBy(c => c.Id).Select(c => c.LastName);
        // PaymentCredit is excluded from CurrentBalance, so "None" sorts as 0; ties go by Id.
        var zero = ById(groupOnly, none, tie).ToList();
        Assert.Equal(["Full", .. zero, "Half"], await Names("balance"));
        Assert.Equal(["Half", .. zero, "Full"], await Names("balanceDesc"));
        var page = await portal.GetClientsPageAsync(new PortalPageFilter { Sort = "balanceDesc", PageSize = 1 });
        Assert.Equal(40, Assert.Single(page.Items).CurrentBalance);

        // Half (5/10) and Tie (4/8) share 0.5; clients without a personal/semi package are last both ways.
        var halfTie = ById(half, tie).ToList();
        var noPersonal = ById(groupOnly, none).ToList();
        Assert.Equal([.. halfTie, "Full", .. noPersonal], await Names("packageUsage"));
        Assert.Equal(["Full", .. halfTie, .. noPersonal], await Names("packageUsageDesc"));
        Assert.DoesNotContain("Foreign", await Names("balanceDesc"));
    }

    [PostgresFact]
    public async Task PaymentListFiltersByMethodAndAmountAndSortsByPaymentDate()
    {
        await using var database = await TestDatabase.CreateAsync();
        await using var db = database.Context();
        var owner = new User { Email = "list-owner@test.invalid" };
        var client = new Client { FirstName = "Pay", LastName = "Er", Location = new Location { Name = "Studio" } };
        var day = new DateTime(2026, 10, 1, 10, 0, 0, DateTimeKind.Utc);
        ClientPayment Pay(decimal amount, PaymentMethod method, int paidDay, int createdDay) => new()
        {
            Client = client, Amount = amount, Method = method, Status = ClientPaymentStatus.Confirmed, Source = ClientPaymentSource.StaffEntry,
            PaymentDate = day.AddDays(paidDay), CreatedAt = day.AddDays(createdDay)
        };
        var cashOld = Pay(100, PaymentMethod.Cash, 0, 9);
        var cashNew = Pay(300, PaymentMethod.Cash, 5, 1);
        var blik = Pay(200, PaymentMethod.Blik, 3, 3);
        var cashSame = Pay(300, PaymentMethod.Cash, 2, 2);
        db.AddRange(owner, cashOld, cashNew, blik, cashSame);
        await db.SaveChangesAsync();
        var billing = new ClientPaymentService(db, new PortalSecurityTests.Actor(owner.Id, "Owner"), null!, Options.Create(new TpaySettings()));
        async Task<List<int>> Ids(ClientPaymentFilterDto filter) => (await billing.GetPaymentsAsync(filter)).Items.Select(p => p.Id).ToList();

        // Default and date sorts follow PaymentDate, not CreatedAt.
        Assert.Equal([cashNew.Id, blik.Id, cashSame.Id, cashOld.Id], await Ids(new ClientPaymentFilterDto()));
        Assert.Equal([cashOld.Id, cashSame.Id, blik.Id, cashNew.Id], await Ids(new ClientPaymentFilterDto { Sort = "paymentDate" }));
        Assert.Equal([cashNew.Id, cashSame.Id, blik.Id, cashOld.Id], await Ids(new ClientPaymentFilterDto { Sort = "amountDesc" }));
        Assert.Equal([cashOld.Id, blik.Id, cashNew.Id, cashSame.Id], await Ids(new ClientPaymentFilterDto { Sort = "amount" }));

        var cash = await billing.GetPaymentsAsync(new ClientPaymentFilterDto { Method = PaymentMethod.Cash, AmountMin = 150, AmountMax = 300 });
        Assert.Equal(2, cash.TotalCount);
        Assert.Equal([cashNew.Id, cashSame.Id], cash.Items.Select(p => p.Id));
        Assert.Equal([blik.Id], await Ids(new ClientPaymentFilterDto { Method = PaymentMethod.Blik, From = day.AddDays(3), To = day.AddDays(3) }));
        await Assert.ThrowsAsync<System.ComponentModel.DataAnnotations.ValidationException>(() =>
            billing.GetPaymentsAsync(new ClientPaymentFilterDto { AmountMin = 300, AmountMax = 100 }));
    }
}
