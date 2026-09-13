using Microsoft.EntityFrameworkCore;
using StudioCRM.Domain.Entities;
using StudioCRM.Domain.Enums;
using StudioCRM.Infrastructure.Services;

namespace StudioCRM.Tests.IntegrationTests;

public class SessionAccountingCorrectionTests
{
    [PostgresFact]
    public async Task RevertingLastSessionReactivatesPreviousPackageAndSuspendsUnusedRenewal()
    {
        await using var database = await TestDatabase.CreateAsync();
        await using var db = database.Context();

        var trainerUser = new User { Email = "trainer-correction@example.test" };
        var location = new Location { Name = "Klaj", City = "Klaj" };
        var package = new Package
        {
            Name = "Personal 4",
            Price = 400,
            SessionsLimit = 4,
            DurationDays = 30,
            BillingType = SessionBillingType.OneToOne
        };
        db.AddRange(trainerUser, location, package);
        await db.SaveChangesAsync();

        var trainer = new Trainer { UserId = trainerUser.Id };
        db.Add(trainer);
        await db.SaveChangesAsync();

        var client = new Client
        {
            LocationId = location.Id,
            TrainerId = trainer.Id,
            ActivePackageId = package.Id,
            FirstName = "Anna",
            LastName = "Nowak",
            Email = "anna-correction@example.test",
            Status = "Active",
            BillingStatus = PaymentStatus.Paid.ToString()
        };
        db.Add(client);
        await db.SaveChangesAsync();

        var previous = new ClientPackage
        {
            ClientId = client.Id,
            PackageId = package.Id,
            Name = package.Name,
            TotalSessions = 4,
            UsedSessions = 4,
            TotalPrice = package.Price,
            OriginalPrice = package.Price,
            AmountPaid = package.Price,
            ExpectedUnitPrice = 100,
            ExpectedBillingType = SessionBillingType.OneToOne,
            PaymentStatus = PaymentStatus.Paid,
            PurchaseDate = DateTime.UtcNow.AddMonths(-1),
            IsActive = false
        };
        db.Add(previous);
        await db.SaveChangesAsync();

        var renewal = new ClientPackage
        {
            ClientId = client.Id,
            PackageId = package.Id,
            Name = package.Name,
            TotalSessions = 4,
            UsedSessions = 0,
            TotalPrice = package.Price,
            OriginalPrice = package.Price,
            AmountPaid = package.Price,
            ExpectedUnitPrice = 100,
            ExpectedBillingType = SessionBillingType.OneToOne,
            PaymentStatus = PaymentStatus.Paid,
            PurchaseDate = DateTime.UtcNow,
            PreviousClientPackageId = previous.Id,
            IsActive = true
        };
        var session = new Session
        {
            Title = "Anna N",
            TrainerId = trainer.Id,
            LocationId = location.Id,
            StartAt = DateTime.UtcNow.AddHours(-2),
            EndAt = DateTime.UtcNow.AddHours(-1),
            Status = "Completed",
            PlannedSessionType = SessionBillingType.OneToOne.ToString(),
            ActualSessionType = SessionBillingType.OneToOne.ToString(),
            ActualParticipantsCount = 1,
            CompletedAt = DateTime.UtcNow.AddHours(-1)
        };
        db.AddRange(renewal, session);
        await db.SaveChangesAsync();

        var participant = new SessionParticipant
        {
            SessionId = session.Id,
            ClientId = client.Id,
            PackageId = package.Id,
            ClientPackageId = previous.Id,
            AttendanceStatus = "Present",
            CountsAgainstPackage = true,
            IsCountedFromPackage = true,
            SessionsCharged = 1,
            PlannedBillingType = SessionBillingType.OneToOne,
            ActualBillingType = SessionBillingType.OneToOne,
            ExpectedUnitPrice = 100,
            ActualUnitPrice = 100,
            BalanceDifference = 0
        };
        db.Add(participant);
        await db.SaveChangesAsync();

        await using var transaction = await db.Database.BeginTransactionAsync();
        await SessionAccountingCorrectionManager.RevertAsync(db, session);
        await db.SaveChangesAsync();
        await transaction.CommitAsync();

        db.ChangeTracker.Clear();
        previous = await db.ClientPackages.SingleAsync(x => x.Id == previous.Id);
        renewal = await db.ClientPackages.SingleAsync(x => x.Id == renewal.Id);
        participant = await db.SessionParticipants.SingleAsync(x => x.Id == participant.Id);
        client = await db.Clients.SingleAsync(x => x.Id == client.Id);

        Assert.True(previous.IsActive);
        Assert.Equal(3, previous.UsedSessions);
        Assert.False(renewal.IsActive);
        Assert.Equal(PaymentStatus.Paid, renewal.PaymentStatus);
        Assert.Equal(package.Id, client.ActivePackageId);
        Assert.False(participant.IsCountedFromPackage);
        Assert.Null(participant.ClientPackageId);
    }
}
