using Microsoft.EntityFrameworkCore;
using StudioCRM.Application.ClientPackages.Services;
using StudioCRM.Application.DTOs.ClientPackages;
using StudioCRM.Application.DTOs.Settings;
using StudioCRM.Application.Interfaces;
using StudioCRM.Domain.Entities;
using StudioCRM.Domain.Enums;
using StudioCRM.Infrastructure.Persistence;
using StudioCRM.Infrastructure.Services;

namespace StudioCRM.Tests.IntegrationTests;

public class PeriodPackageTests
{
    private sealed class Settings : IStudioSettingsService
    {
        public Task<OwnerSettingsDto> GetOwnerSettingsAsync() => Task.FromResult(new OwnerSettingsDto { DefaultPackageValidityDays = 90, DefaultPaymentDueDays = 4 });
        public Task<OwnerSettingsDto> UpdateOwnerSettingsAsync(UpdateOwnerSettingsDto r) => throw new NotSupportedException();
    }
    private static async Task<(Client, Package, PortalSecurityTests.Actor)> Seed(StudioCRMDbContext db)
    {
        var location = new Location { Name = "Period studio" };
        var trainer = new Trainer { User = new User { Email = "period-owner@example.test" } };
        var client = new Client { FirstName = "Period", LastName = "Client", Location = location, Trainer = trainer };
        db.Add(client); await db.SaveChangesAsync();
        var actor = new PortalSecurityTests.Actor(trainer.UserId, "Owner");
        var offer = await new PackageService(db, actor).CreateAsync(new() { Name = "Dietetyka", BillingMode = PackageBillingMode.PeriodBased,
            DurationDays = 30, Price = 200, LocationId = location.Id });
        return (client, await db.Packages.SingleAsync(p => p.Id == offer.Id), actor);
    }

    [PostgresFact]
    public async Task PeriodCoexistsWithTrainingAndPaymentDoesNotMoveOwnerDates()
    {
        await using var database = await TestDatabase.CreateAsync(); await using var db = database.Context();
        var (client, offer, actor) = await Seed(db);
        var training = new ClientPackage { Client = client, Package = new Package { Name = "Training", SessionsLimit = 4 }, TotalSessions = 4, PaymentStatus = PaymentStatus.Paid };
        db.Add(training); await db.SaveChangesAsync(); client.ActivePackageId = training.PackageId; await db.SaveChangesAsync();
        var owner = new ClientPackageService(db, actor, new Settings());
        var startsOn = PeriodPackageService.Today().AddDays(3);
        var id = await owner.CreateAsync(new() { ClientId = client.Id, PackageId = offer.Id, StartsOn = startsOn });
        var pass = await db.ClientPackages.SingleAsync(p => p.Id == id);
        Assert.Equal(0, pass.TotalSessions); Assert.Equal(0, pass.ExpectedUnitPrice);
        Assert.Equal(training.PackageId, client.ActivePackageId); Assert.True(training.IsActive);
        var from = PeriodPackageService.StartOfDay(startsOn); var to = PeriodPackageService.AddDays(from, 30);
        Assert.Equal(from, pass.PeriodStartsAt); Assert.Equal(to, pass.ValidUntil);
        var periods = new PeriodPackageService(db, actor, new Settings());
        var item = Assert.Single((await periods.ListAsync(client.Id, 1, 25)).Items);
        Assert.Equal("PeriodBased", item.PackageType); Assert.Equal("Scheduled", item.PeriodStatus);
        Assert.Equal("", item.ExpectedBillingType);
        await PortalSecurityTests.Payments(db, actor).CreatePaymentAsStaffAsync(new() { ClientId = client.Id, ClientPackageId = id, Amount = 200, Method = PaymentMethod.Cash });
        Assert.Equal(PaymentStatus.Paid, pass.PaymentStatus); Assert.Equal(from, pass.PeriodStartsAt); Assert.Equal(to, pass.ValidUntil);
        Assert.Equal(training.PackageId, client.ActivePackageId);
        Assert.Equal(training.Id, (await PortalSecurityTests.Payments(db, actor).GetActivePackageAsync(client.Id))!.ClientPackageId);
        await new SubscriptionService(db, actor, new Settings()).RenewAfterCompletedCycleAsync(id);
        Assert.True(pass.IsActive); Assert.Equal(2, await db.ClientPackages.CountAsync());
        var session = new Session { TrainerId = client.TrainerId!.Value, LocationId = client.LocationId, StartAt = DateTime.UtcNow.AddDays(-1), EndAt = DateTime.UtcNow };
        var participant = new SessionParticipant { Client = client, Session = session };
        db.Add(participant); await db.SaveChangesAsync();
        await Assert.ThrowsAsync<InvalidOperationException>(() => PortalSecurityTests.Sessions(db, actor).CountSessionFromPackageAsync(new() { SessionParticipantId = participant.Id, ClientPackageId = id, ActualBillingType = SessionBillingType.OneToOne }));
        await Assert.ThrowsAsync<InvalidOperationException>(() => owner.ActivateAsync(client.Id, id));
        await Assert.ThrowsAsync<InvalidOperationException>(() => new SubscriptionService(db, actor, new Settings()).SetNextPackageAsync(client.Id, new() { PackageId = offer.Id }));
        var subscription = new SubscriptionService(db, actor, new Settings());
        await subscription.CancelRenewalAsync(client.Id);
        training.UsedSessions = training.TotalSessions; await db.SaveChangesAsync();
        await subscription.RenewAfterCompletedCycleAsync(training.Id);
        Assert.Equal("Active", client.Status);
        Assert.True(pass.IsActive);
        await Assert.ThrowsAsync<KeyNotFoundException>(() => new PeriodPackageService(db, new PortalSecurityTests.Actor(999, "Client"), new Settings()).ListAsync(client.Id, 1, 25));
    }

    [PostgresFact]
    public async Task PeriodRenewalIsIdempotentAcrossConcurrentWorkersAndKeepsPriceSnapshot()
    {
        await using var database = await TestDatabase.CreateAsync(); await using var db = database.Context();
        var (client, offer, actor) = await Seed(db);
        var id = await new ClientPackageService(db, actor, new Settings()).CreateAsync(new() { ClientId = client.Id, PackageId = offer.Id,
            StartsOn = PeriodPackageService.Today().AddDays(-31), AutoRenew = true });
        var before = await db.ClientPackages.SingleAsync(); var previousEnd = before.ValidUntil;
        offer.Price = 999; await db.SaveChangesAsync();
        await using var a = database.Context(); await using var b = database.Context();
        await Task.WhenAll(new PeriodPackageService(a, actor, new Settings()).ProcessDueAsync(), new PeriodPackageService(b, actor, new Settings()).ProcessDueAsync());
        db.ChangeTracker.Clear();
        var old = await db.ClientPackages.SingleAsync(p => p.Id == id);
        var next = await db.ClientPackages.SingleAsync(p => p.PreviousClientPackageId == id);
        Assert.False(old.IsActive); Assert.True(next.IsActive); Assert.Equal(200, next.TotalPrice);
        Assert.Equal(previousEnd, next.PeriodStartsAt); Assert.Equal(PeriodPackageService.AddDays(previousEnd!.Value, 30), next.ValidUntil);
        Assert.Equal(PeriodPackageService.AddDays(previousEnd.Value, 4), next.PaymentDueDate);
        Assert.Equal(0, next.TotalSessions); Assert.Equal(PaymentStatus.Unpaid, next.PaymentStatus);
        Assert.Equal(0, await new PeriodPackageService(db, actor, new Settings()).ProcessDueAsync());
        Assert.Equal(2, await db.ClientPackages.CountAsync());
        Assert.Single(await db.ClientAuditEntries.Where(x => x.Action == "PeriodServiceRenewed").ToListAsync());
    }

    [PostgresFact]
    public async Task OwnerCanDisableRenewalAndExpiredPaymentDoesNotReactivateService()
    {
        await using var database = await TestDatabase.CreateAsync(); await using var db = database.Context();
        var (client, offer, actor) = await Seed(db);
        var id = await new ClientPackageService(db, actor, new Settings()).CreateAsync(new() { ClientId = client.Id, PackageId = offer.Id, AutoRenew = true });
        var p = await db.ClientPackages.SingleAsync();
        Assert.Equal(PeriodPackageService.StartOfDay(PeriodPackageService.Today()), p.PeriodStartsAt);
        var service = new PeriodPackageService(db, actor, new Settings());
        await service.SetRenewalAsync(client.Id, id, false);
        p.PeriodStartsAt = PeriodPackageService.StartOfDay(PeriodPackageService.Today().AddDays(-31));
        p.ValidUntil = PeriodPackageService.AddDays(p.PeriodStartsAt.Value, 30); await db.SaveChangesAsync();
        Assert.Equal(1, await service.ProcessDueAsync()); Assert.False(p.IsActive);
        await PortalSecurityTests.Payments(db, actor).CreatePaymentAsStaffAsync(new() { ClientId = client.Id, ClientPackageId = id, Amount = 200, Method = PaymentMethod.Cash });
        Assert.False(p.IsActive); Assert.Single(await db.ClientPackages.ToListAsync());
        Assert.Equal("Expired", Assert.Single((await service.ListAsync(client.Id, 1, 25)).Items).PeriodStatus);
    }

    [PostgresFact]
    public async Task RejectsOverlappingAssignmentAndModeChange()
    {
        await using var database = await TestDatabase.CreateAsync(); await using var db = database.Context();
        var (client, offer, actor) = await Seed(db);
        var service = new ClientPackageService(db, actor, new Settings());
        await service.CreateAsync(new() { ClientId = client.Id, PackageId = offer.Id });
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateAsync(new() { ClientId = client.Id, PackageId = offer.Id }));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => new ClientPackageService(db, new PortalSecurityTests.Actor(actor.UserId!.Value), new Settings()).CreateAsync(new() { ClientId = client.Id, PackageId = offer.Id }));
        await Assert.ThrowsAsync<InvalidOperationException>(() => new PackageService(db, actor).UpdateAsync(offer.Id, new() { BillingMode = PackageBillingMode.SessionBased }));
        await Assert.ThrowsAsync<InvalidOperationException>(() => new PackageService(db, actor).CreateAsync(new() { BillingMode = PackageBillingMode.PeriodBased, DurationDays = 0 }));
    }

    [Fact]
    public void PeriodUsesWarsawCalendarDaysAcrossDaylightSavingTime()
    {
        var from = PeriodPackageService.StartOfDay(new DateOnly(2026, 10, 24));
        var to = PeriodPackageService.AddDays(from, 2);
        Assert.Equal(new DateTime(2026, 10, 25, 23, 0, 0, DateTimeKind.Utc), to);
        Assert.Equal(49, (to - from).TotalHours);
    }
}
