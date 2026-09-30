using Microsoft.Extensions.Options;
using StudioCRM.Application.Settings;
using StudioCRM.Domain.Entities;
using StudioCRM.Domain.Enums;
using StudioCRM.Infrastructure.Services;

namespace StudioCRM.Tests.IntegrationTests;

public class ClientPackagePresentationTests
{
    [PostgresFact]
    public async Task NextSessionUsesExactPackageAndExcludesCancelledDeletedPastAndCompletedSessions()
    {
        await using var database = await TestDatabase.CreateAsync();
        await using var db = database.Context();
        var client = new Client { FirstName = "Anna", Location = new Location { Name = "Studio" } };
        var trainer = new Trainer { User = new User { Email = "trainer@example.test" } };
        var owner = new User { Email = "owner@example.test" };
        ClientPackage Package(SessionBillingType type) => new()
        {
            Client = client, Package = new Package { Name = "Template" }, Name = "Cycle",
            ExpectedBillingType = type, PurchaseDate = DateTime.UtcNow, TotalSessions = 10
        };
        var group = Package(SessionBillingType.Group);
        var secondGroup = Package(SessionBillingType.Group);
        var individual = Package(SessionBillingType.OneToOne);
        var semi = Package(SessionBillingType.ThreeToOne);
        var next = DateTime.UtcNow.Date.AddDays(5);
        void Book(ClientPackage package, DateTime start, string status = "Planned", string attendance = "Planned", bool deleted = false)
        {
            db.Add(new SessionParticipant
            {
                Client = client, ClientPackage = package, AttendanceStatus = attendance,
                Session = new Session { Location = client.Location, Trainer = trainer, StartAt = start,
                    EndAt = start.AddHours(1), Status = status, IsDeleted = deleted }
            });
        }
        Book(group, next.AddDays(-10));
        Book(group, next.AddDays(-1), "Cancelled");
        Book(group, next.AddDays(-1), "Completed");
        Book(group, next.AddDays(-1), attendance: "CancelledInTime");
        Book(group, next.AddDays(-1), attendance: "CancelledLate");
        Book(group, next.AddDays(-1), deleted: true);
        Book(group, next);
        Book(group, next.AddDays(1));
        Book(secondGroup, next.AddDays(-2));
        db.AddRange(owner, individual, semi);
        await db.SaveChangesAsync();
        var staff = new ClientLifecycleV2Tests.Staff(owner.Id);
        var billing = new ClientPaymentService(db, staff, null!, Options.Create(new TpaySettings()));
        var summary = await billing.GetClientSummaryAsync(client.Id);
        Assert.Equal(next, summary.Packages.Single(p => p.ClientPackageId == group.Id).NextSessionAt);
        Assert.Equal(next.AddDays(-2), summary.Packages.Single(p => p.ClientPackageId == secondGroup.Id).NextSessionAt);
        Assert.Equal("Group", summary.Packages.Single(p => p.ClientPackageId == group.Id).PackageType);
        Assert.Equal("SemiPersonal", summary.Packages.Single(p => p.ClientPackageId == semi.Id).PackageType);
        Assert.Equal("Individual", summary.Packages.Single(p => p.ClientPackageId == individual.Id).PackageType);
        Assert.Null(summary.Packages.Single(p => p.ClientPackageId == individual.Id).NextSessionAt);
        var clients = new ClientService(db, staff, null!, null!);
        var history = await clients.GetPackageHistoryAsync(client.Id);
        Assert.Equal(next, history.Items.Single(p => p.ClientPackageId == group.Id).NextSessionAt);

        // Legacy archived records may still have an active package.
        group.PurchaseDate = DateTime.UtcNow.AddDays(1);
        client.IsDeleted = true;
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        var active = await billing.GetActivePackageAsync(client.Id);
        Assert.NotNull(active);
        Assert.Equal(group.Id, active.ClientPackageId);
        Assert.Equal(next, active.NextSessionAt);
        Assert.NotNull(await clients.GetByIdAsync(client.Id));
        Assert.Equal(4, (await clients.GetPackageHistoryAsync(client.Id)).TotalCount);
    }
}
