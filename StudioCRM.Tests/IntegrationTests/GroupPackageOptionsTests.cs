using Microsoft.EntityFrameworkCore;
using StudioCRM.Application.DTOs.Clients;
using StudioCRM.Domain.Entities;
using StudioCRM.Domain.Enums;
using StudioCRM.Infrastructure.Services;

namespace StudioCRM.Tests.IntegrationTests;

public class GroupPackageOptionsTests
{
    [PostgresFact]
    public async Task GroupFilterUsesMembershipRatherThanHomeLocationAndKeepsLegacyFilter()
    {
        await using var database = await TestDatabase.CreateAsync();
        await using var db = database.Context();
        var home = new Location { Name = "Home" };
        var group = new Location { Name = "Group" };
        var client = new Client { FirstName = "Anna", LastName = "Membership", Location = home };
        db.Add(new ClientLocationMembership { Client = client, Location = group, GroupAccessEnabled = true });
        db.Add(new Client { FirstName = "Anna", LastName = "No access", Location = group });
        await db.SaveChangesAsync();
        var service = new ClientService(db, new PortalSecurityTests.Actor(1, "Owner"), null!, null!);
        var result = await service.GetFilteredAsync(new() { LocationId = group.Id, GroupAccessEnabled = true, Search = "anna" });
        Assert.Equal(client.Id, Assert.Single(result).Id);
        Assert.DoesNotContain(await service.GetFilteredAsync(new() { LocationId = group.Id }), c => c.Id == client.Id);
        Assert.Empty(await service.GetFilteredAsync(new() { LocationId = home.Id, GroupAccessEnabled = true }));
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.GetFilteredAsync(new() { GroupAccessEnabled = true }));
    }

    [PostgresFact]
    public async Task OptionsAccountForReservationsValidityPaymentAndExplicitSelection()
    {
        await using var database = await TestDatabase.CreateAsync();
        await using var db = database.Context();
        var location = new Location { Name = "Group" };
        var other = new Location { Name = "Other" };
        var trainer = new Trainer { User = new User { Email = "options@example.test" } };
        var client = new Client { FirstName = "Anna", Location = location, Trainer = trainer };
        var session = new Session { Title = "Group", Location = location, Trainer = trainer, PlannedSessionType = "Group",
            StartAt = DateTime.UtcNow.AddDays(2), EndAt = DateTime.UtcNow.AddDays(2).AddHours(1) };
        var catalog = new Package { Name = "Group offer", BillingType = SessionBillingType.Group, Location = location,
            Price = 100, DurationDays = 30, SessionsLimit = 4 };
        db.AddRange(session, other, new ClientLocationMembership { Client = client, Location = location, GroupAccessEnabled = true });
        ClientPackage Pass(string name) => new() { Client = client, Package = catalog, Name = name, TotalPrice = 100, TotalSessions = 4,
            ExpectedBillingType = SessionBillingType.Group, PaymentStatus = PaymentStatus.Paid, Location = location, ValidUntil = session.StartAt.AddDays(1) };
        var valid = Pass("Valid"); var reserved = Pass("Reserved"); var expired = Pass("Expired");
        var unpaid = Pass("Unpaid"); var wrongLocation = Pass("Other location"); var closed = Pass("Closed");
        reserved.UsedSessions = 3; expired.ValidUntil = session.StartAt.AddMinutes(-1);
        unpaid.PaymentStatus = PaymentStatus.Unpaid; unpaid.IsActive = false;
        wrongLocation.Location = other; closed.ClosureDisposition = "Closed";
        var personal = Pass("Personal"); personal.ExpectedBillingType = SessionBillingType.OneToOne;
        db.AddRange(valid, reserved, expired, unpaid, wrongLocation, closed, personal);
        db.Add(new SessionParticipant { Client = client, ClientPackage = reserved, CountsAgainstPackage = true, SessionsCharged = 1,
            Session = new Session { Trainer = trainer, Location = location, PlannedSessionType = "Group", StartAt = session.StartAt.AddDays(-1), EndAt = session.EndAt.AddDays(-1) } });
        // Cancelled and already consumed entries must not reserve a second time.
        db.Add(new SessionParticipant { Client = client, ClientPackage = valid, CountsAgainstPackage = true, SessionsCharged = 4, AttendanceStatus = "CancelledInTime",
            Session = new Session { Trainer = trainer, Location = location, StartAt = session.StartAt, EndAt = session.EndAt } });
        await db.SaveChangesAsync();
        var owner = new PortalSecurityTests.Actor(trainer.UserId, "Owner");
        var service = new GroupEnrollmentService(db, owner, null!);
        var result = await service.GetPackageOptionsAsync(session.Id, client.Id);
        Assert.Equal(100, Assert.Single(result.Offers).Price);
        Assert.DoesNotContain(result.OwnedPackages, p => p.ClientPackageId == personal.Id);
        Assert.True(result.OwnedPackages.Single(p => p.ClientPackageId == valid.Id).IsEligible);
        var exhausted = result.OwnedPackages.Single(p => p.ClientPackageId == reserved.Id);
        Assert.Equal(1, exhausted.ReservedEntries); Assert.Equal(0, exhausted.AvailableEntries);
        Assert.False(exhausted.CanSelect); Assert.Contains("NoAvailableEntries", exhausted.Reasons);
        Assert.Contains("ExpiredOnSessionDate", result.OwnedPackages.Single(p => p.ClientPackageId == expired.Id).Reasons);
        Assert.Contains("LocationMismatch", result.OwnedPackages.Single(p => p.ClientPackageId == wrongLocation.Id).Reasons);
        Assert.False(result.OwnedPackages.Single(p => p.ClientPackageId == closed.Id).CanSelect);
        var awaiting = result.OwnedPackages.Single(p => p.ClientPackageId == unpaid.Id);
        Assert.True(awaiting.CanSelect); Assert.False(awaiting.IsEligible); Assert.Equal(100, awaiting.AmountDue);
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.PrepareAsync(session.Id, new() { ClientId = client.Id, PackageId = catalog.Id, ClientPackageId = reserved.Id }));
        var enrollment = await service.PrepareAsync(session.Id, new() { ClientId = client.Id, PackageId = catalog.Id, ClientPackageId = valid.Id });
        Assert.Equal(valid.Id, enrollment.ClientPackageId);
        Assert.Equal("ReadyToBook", enrollment.Status);
        Assert.False(enrollment.ReservesSeat);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => new GroupEnrollmentService(db, new PortalSecurityTests.Actor(trainer.UserId), null!).GetPackageOptionsAsync(session.Id, client.Id));
        await Assert.ThrowsAsync<KeyNotFoundException>(() => service.GetPackageOptionsAsync(session.Id, int.MaxValue));
        var membership = await db.ClientLocationMemberships.SingleAsync(); membership.GroupAccessEnabled = false; await db.SaveChangesAsync();
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.GetPackageOptionsAsync(session.Id, client.Id));
    }
}
