using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using StudioCRM.Application.ClientPackages.Services;
using StudioCRM.Application.DTOs.Billing;
using StudioCRM.Application.DTOs.ClientPackages;
using StudioCRM.Application.DTOs.Settings;
using StudioCRM.Application.Interfaces;
using StudioCRM.Application.Settings;
using StudioCRM.Domain.Entities;
using StudioCRM.Domain.Enums;
using StudioCRM.Infrastructure.Services;

namespace StudioCRM.Tests.IntegrationTests;

public class PackageManagementTests
{
    [PostgresFact]
    public async Task ImportIsIdempotentAndReplacementPreservesHistory()
    {
        await using var database = await TestDatabase.CreateAsync();
        await using var db = database.Context();
        var owner = new User { Email = "package-owner@example.test" };
        var client = new Client { FirstName = "Anna", Location = new Location { Name = "Studio" } };
        var template = new Package { Name = "10 wejsc", Price = 100, SessionsLimit = 10, BillingType = SessionBillingType.OneToOne };
        db.AddRange(owner, client, template); await db.SaveChangesAsync();
        var staff = new ClientLifecycleV2Tests.Staff(owner.Id);
        var service = new ClientPackageService(db, staff, new Settings());
        var request = new ImportClientPackageRequest
        {
            RequestId = Guid.NewGuid(), Reason = "Migration", UsedSessions = 3, AmountPaid = 100,
            Package = new CreateClientPackageRequest { ClientId = client.Id, PackageId = template.Id }
        };
        var id = await service.ImportAsync(request);
        Assert.Equal(id, await service.ImportAsync(request));
        Assert.Empty(await db.ClientPayments.ToListAsync());
        var imported = await db.ClientPackages.SingleAsync();
        Assert.Equal(3, imported.UsedSessions); Assert.Equal(100m, imported.AmountPaid);
        var preview = await service.GetManagementPreviewAsync(client.Id, id);
        Assert.False(preview.CanDelete); Assert.False(preview.CanEdit); Assert.True(preview.CanClose);
        var replacement = await service.CloseAsync(client.Id, id, new ClosePackageRequest
        {
            ExpectedVersion = preview.Version, Reason = "Change package", DebtDisposition = "WaiveDue",
            FundsDisposition = "Balance", SettlementAmount = 70,
            Replacement = new CreateClientPackageRequest { ClientId = client.Id, PackageId = template.Id }
        });
        Assert.NotNull(replacement);
        Assert.Equal("TransferredToBalance", imported.ClosureDisposition);
        Assert.Equal(100m, imported.AmountPaid);
        var next = await db.ClientPackages.SingleAsync(x => x.Id == replacement);
        Assert.Equal(70m, next.BalanceApplied); Assert.Equal(30m, next.TotalPrice);
        Assert.Equal(2, await db.ClientPackages.CountAsync());
    }

    [PostgresFact]
    public async Task SummaryIncludesInactiveDebtAndGroupDoesNotReplacePersonalCycle()
    {
        await using var database = await TestDatabase.CreateAsync();
        await using var db = database.Context();
        var owner = new User { Email = "summary-owner@example.test" };
        var client = new Client { FirstName = "Anna", Location = new Location { Name = "Studio" }, SubscriptionAutoRenewEnabled = false };
        var template = new Package { Name = "Template" };
        var personal = new ClientPackage { Client = client, Package = template, Name = "Personal", TotalSessions = 10, TotalPrice = 100, ExpectedBillingType = SessionBillingType.OneToOne, PurchaseDate = DateTime.UtcNow.AddDays(-2), PaymentStatus = PaymentStatus.Unpaid };
        var group = new ClientPackage { Client = client, Package = template, Name = "Group", TotalPrice = 40, ExpectedBillingType = SessionBillingType.Group, PurchaseDate = DateTime.UtcNow };
        var old = new ClientPackage { Client = client, Package = template, Name = "Old", TotalPrice = 20, IsActive = false, PurchaseDate = DateTime.UtcNow.AddDays(-5) };
        db.AddRange(owner, personal, group, old); await db.SaveChangesAsync();
        var staff = new ClientLifecycleV2Tests.Staff(owner.Id);
        var payments = new ClientPaymentService(db, staff, null!, Options.Create(new TpaySettings()));
        var summary = await payments.GetClientSummaryAsync(client.Id);
        Assert.Equal(160m, summary.TotalAmountDue); Assert.Equal(personal.Id, summary.ActiveClientPackageId);
        var subscription = await new SubscriptionService(db, staff, new Settings()).GetClientSubscriptionAsync(client.Id);
        Assert.Equal(personal.Id, subscription.CurrentCycle!.ClientPackageId);
        Assert.Null(subscription.NextPackage); Assert.NotEqual("Cancelled", subscription.Status);
        var manager = new ClientPackageService(db, staff, new Settings());
        var preview = await manager.GetManagementPreviewAsync(client.Id, personal.Id);
        personal.AmountPaid = 10; await db.SaveChangesAsync();
        await Assert.ThrowsAsync<InvalidOperationException>(() => manager.CloseAsync(client.Id, personal.Id,
            new ClosePackageRequest { ExpectedVersion = preview.Version, Reason = "Stale preview" }));
    }

    [PostgresFact]
    public async Task FailedReplacementRollsBackClosureAndBalanceCredit()
    {
        await using var database = await TestDatabase.CreateAsync();
        await using var db = database.Context();
        var owner = new User { Email = "rollback-owner@example.test" };
        var client = new Client { FirstName = "Anna", Location = new Location { Name = "Studio" } };
        var package = new ClientPackage
        {
            Client = client, Package = new Package { Name = "Old" }, Name = "Old", TotalSessions = 10,
            TotalPrice = 100, AmountPaid = 100, ExpectedBillingType = SessionBillingType.OneToOne,
            PurchaseDate = DateTime.UtcNow, IsActive = true
        };
        db.AddRange(owner, package); await db.SaveChangesAsync();
        var service = new ClientPackageService(db, new ClientLifecycleV2Tests.Staff(owner.Id), new Settings());
        var preview = await service.GetManagementPreviewAsync(client.Id, package.Id);
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.CloseAsync(client.Id, package.Id,
            new ClosePackageRequest
            {
                ExpectedVersion = preview.Version, Reason = "Bad replacement", DebtDisposition = "WaiveDue",
                FundsDisposition = "Balance", SettlementAmount = 50,
                Replacement = new CreateClientPackageRequest { ClientId = client.Id, PackageId = int.MaxValue }
            }));
        db.ChangeTracker.Clear();
        var unchanged = await db.ClientPackages.SingleAsync();
        Assert.True(unchanged.IsActive); Assert.Null(unchanged.ClosureDisposition);
        Assert.Empty(await db.ClientBalanceTransactions.ToListAsync());
        Assert.Empty(await db.ClientAuditEntries.ToListAsync());
    }

    [PostgresFact]
    public async Task PriceCorrectionBelowPaidMovesSurplusThroughThePayment()
    {
        await using var database = await TestDatabase.CreateAsync();
        await using var db = database.Context();
        var owner = new User { Email = "surplus-owner@example.test" };
        var client = new Client { FirstName = "Teresa", Location = new Location { Name = "Studio" } };
        var template = new Package { Name = "4 personalne", Price = 680, SessionsLimit = 4, BillingType = SessionBillingType.OneToOne };
        var pass = new ClientPackage { Client = client, Package = template, TotalSessions = 4, TotalPrice = 680, OriginalPrice = 680,
            AmountPaid = 680, ExpectedUnitPrice = 170, PaymentStatus = PaymentStatus.Paid, IsActive = true };
        var payment = new ClientPayment { Client = client, ClientPackage = pass, Amount = 680, AppliedToPackageAmount = 680,
            Status = ClientPaymentStatus.Confirmed, Method = PaymentMethod.BankTransfer };
        db.AddRange(owner, payment); await db.SaveChangesAsync();
        var service = new ClientPackageService(db, new ClientLifecycleV2Tests.Staff(owner.Id), new Settings());
        var preview = await service.GetManagementPreviewAsync(client.Id, pass.Id);

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.CorrectAsync(client.Id, pass.Id,
            new PackageChangeRequest { ExpectedVersion = preview.Version, Reason = "Cena semi", TotalPrice = 540 }));
        var result = await service.CorrectAsync(client.Id, pass.Id, new PackageChangeRequest
        { ExpectedVersion = preview.Version, Reason = "Cena semi", TotalPrice = 540, SurplusDisposition = "ToBalance" });

        Assert.Equal(140m, result.Surplus);
        Assert.Equal(540m, pass.AmountPaid); Assert.Equal(PaymentStatus.Paid, pass.PaymentStatus);
        Assert.Equal(540m, payment.AppliedToPackageAmount); Assert.Equal(140m, payment.BalanceCreditAmount);
        Assert.Equal(140m, await db.ClientBalanceTransactions.Where(t => t.Type == BalanceTransactionType.PaymentOverpayment).SumAsync(t => t.Amount));
        Assert.True(await db.ClientAuditEntries.AnyAsync(a => a.Action == "PackageSurplusToBalance"));
    }

    [PostgresFact]
    public async Task StudioCanKeepSurplusOfImportedPaidPackageWithoutTouchingBalance()
    {
        await using var database = await TestDatabase.CreateAsync();
        await using var db = database.Context();
        var owner = new User { Email = "keep-owner@example.test" };
        var client = new Client { FirstName = "Anna", Location = new Location { Name = "Studio" } };
        var template = new Package { Name = "4 personalne", Price = 680, SessionsLimit = 4, BillingType = SessionBillingType.OneToOne };
        var pass = new ClientPackage { Client = client, Package = template, TotalSessions = 4, TotalPrice = 680, OriginalPrice = 680,
            AmountPaid = 680, ExpectedUnitPrice = 170, PaymentStatus = PaymentStatus.Paid, IsActive = true, RenewalSource = "OpeningBalance" };
        db.AddRange(owner, pass); await db.SaveChangesAsync();
        var service = new ClientPackageService(db, new ClientLifecycleV2Tests.Staff(owner.Id), new Settings());
        var preview = await service.GetManagementPreviewAsync(client.Id, pass.Id);

        var result = await service.CorrectAsync(client.Id, pass.Id, new PackageChangeRequest
        { ExpectedVersion = preview.Version, Reason = "Rozliczenie wewnetrzne", TotalPrice = 600, SurplusDisposition = "KeepByStudio" });

        Assert.Equal(80m, result.Surplus); Assert.Equal(600m, pass.AmountPaid); Assert.Equal(0m, result.AmountDue);
        Assert.Empty(await db.ClientBalanceTransactions.ToListAsync());
        Assert.True(await db.ClientAuditEntries.AnyAsync(a => a.Action == "PackageSurplusKeptByStudio"));
    }

    [PostgresFact]
    public async Task HistoricalPackageIsFinishedInactiveAndTakesItsPaymentWithoutTouchingTheCurrentPackage()
    {
        await using var database = await TestDatabase.CreateAsync();
        await using var db = database.Context();
        var owner = new User { Email = "historical-owner@example.test" };
        var client = new Client { FirstName = "Renata", Location = new Location { Name = "Studio" } };
        var template = new Package { Name = "8 wejsc", Price = 690, SessionsLimit = 8, BillingType = SessionBillingType.OneToOne };
        db.AddRange(owner, client, template); await db.SaveChangesAsync();
        var staff = new ClientLifecycleV2Tests.Staff(owner.Id);
        var service = new ClientPackageService(db, staff, new Settings());
        var currentId = await service.CreateAsync(new CreateClientPackageRequest { ClientId = client.Id, PackageId = template.Id, PurchaseDate = DateTime.UtcNow });

        var request = new HistoricalClientPackageRequest
        {
            RequestId = Guid.NewGuid(), Reason = "Płatność 01.10 za wcześniejszy pakiet", ClientId = client.Id, PackageId = template.Id,
            PurchaseDate = new DateTime(2026, 9, 1, 10, 0, 0, DateTimeKind.Utc), ValidUntil = new DateTime(2026, 9, 30, 10, 0, 0, DateTimeKind.Utc)
        };
        var id = await service.CreateHistoricalAsync(request);
        Assert.Equal(id, await service.CreateHistoricalAsync(request));
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateHistoricalAsync(new HistoricalClientPackageRequest
        {
            RequestId = request.RequestId, Reason = "inna", ClientId = client.Id, PackageId = template.Id, PurchaseDate = request.PurchaseDate
        }));
        db.ChangeTracker.Clear();
        var historical = await db.ClientPackages.SingleAsync(p => p.Id == id);
        Assert.Equal((false, 8, 8, "Historical", 690m, 0m), (historical.IsActive, historical.UsedSessions, historical.TotalSessions,
            historical.RenewalSource, historical.TotalPrice, historical.BalanceApplied));

        var payments = new ClientPaymentService(db, staff, null!, Options.Create(new TpaySettings()));
        var paymentDate = new DateTime(2026, 10, 1, 10, 0, 0, DateTimeKind.Utc);
        var payment = await payments.CreatePaymentAsStaffAsync(new CreateClientPaymentRequest
        { ClientId = client.Id, ClientPackageId = id, Amount = 690, Method = PaymentMethod.BankTransfer, PaymentDate = paymentDate });
        Assert.Equal((paymentDate, 690m, 0m), (payment.PaymentDate, payment.AppliedToPackageAmount, payment.BalanceCreditAmount));

        db.ChangeTracker.Clear();
        historical = await db.ClientPackages.SingleAsync(p => p.Id == id);
        Assert.Equal((PaymentStatus.Paid, false), (historical.PaymentStatus, historical.IsActive));
        var current = await db.ClientPackages.SingleAsync(p => p.Id == currentId);
        Assert.Equal((true, 0m), (current.IsActive, current.AmountPaid));
        var summary = await payments.GetClientSummaryAsync(client.Id);
        Assert.Equal(currentId, summary.ActiveClientPackageId);

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.ActivateAsync(client.Id, id));
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateHistoricalAsync(new HistoricalClientPackageRequest
        { RequestId = Guid.NewGuid(), Reason = "przyszłość", ClientId = client.Id, PackageId = template.Id, PurchaseDate = DateTime.UtcNow.AddDays(2) }));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => new ClientPackageService(db, new PortalSecurityTests.Actor(owner.Id), new Settings())
            .CreateHistoricalAsync(new HistoricalClientPackageRequest { RequestId = Guid.NewGuid(), Reason = "trener", ClientId = client.Id, PackageId = template.Id, PurchaseDate = request.PurchaseDate }));
    }

    private sealed class Settings : IStudioSettingsService
    {
        public Task<OwnerSettingsDto> GetOwnerSettingsAsync() => Task.FromResult(new OwnerSettingsDto { DefaultPackageValidityDays = 90, DefaultPaymentDueDays = 7 });
        public Task<OwnerSettingsDto> UpdateOwnerSettingsAsync(UpdateOwnerSettingsDto request) => throw new NotSupportedException();
    }
}
