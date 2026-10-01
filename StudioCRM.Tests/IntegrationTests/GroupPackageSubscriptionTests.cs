using Microsoft.EntityFrameworkCore;
using StudioCRM.Domain.Entities;
using StudioCRM.Domain.Enums;
using StudioCRM.Infrastructure.Services;

namespace StudioCRM.Tests.IntegrationTests;

public class GroupPackageSubscriptionTests
{
    [PostgresFact]
    public async Task GroupPassNeverBecomesOrRenewsPersonalSubscription()
    {
        await using var database = await TestDatabase.CreateAsync();
        await using var db = database.Context();
        var owner = new User { Email = "owner-group@example.test" };
        var client = new Client { FirstName = "Test", Location = new Location { Name = "Studio" },
            SubscriptionAutoRenewEnabled = true, Status = "Active" };
        var personal = new ClientPackage { Client = client, Package = new Package { Name = "Personal" },
            Name = "Personal", ExpectedBillingType = SessionBillingType.OneToOne,
            TotalSessions = 4, PurchaseDate = DateTime.UtcNow.AddDays(-10), PaymentStatus = PaymentStatus.Paid };
        var group = new ClientPackage { Client = client, Package = new Package { Name = "Group", BillingType = SessionBillingType.Group },
            Name = "Group", ExpectedBillingType = SessionBillingType.Group,
            TotalSessions = 4, UsedSessions = 4, PurchaseDate = DateTime.UtcNow, PaymentStatus = PaymentStatus.Paid };
        db.AddRange(owner, personal, group);
        await db.SaveChangesAsync();
        client.ActivePackageId = personal.PackageId;
        await db.SaveChangesAsync();
        var service = new SubscriptionService(db, new ClientLifecycleV2Tests.Staff(owner.Id), null!);
        var before = await service.GetClientSubscriptionAsync(client.Id);
        Assert.Equal(personal.Id, before.CurrentCycle!.ClientPackageId);
        await service.RenewAfterCompletedCycleAsync(group.Id);
        Assert.False(group.IsActive);
        Assert.True(personal.IsActive);
        Assert.Equal(personal.PackageId, client.ActivePackageId);
        Assert.Equal("Active", client.Status);
        Assert.Equal(2, await db.ClientPackages.CountAsync());
        var cancelledRenewal = await service.CancelRenewalAsync(client.Id);
        Assert.Equal("Active", cancelledRenewal.Status);
        Assert.Null(cancelledRenewal.NextPackage);
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.SetNextPackageAsync(client.Id,
            new() { PackageId = group.PackageId }));
    }
}
