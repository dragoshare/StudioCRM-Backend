using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using StudioCRM.Application.Interfaces;
using StudioCRM.Application.Interfaces.Calendar;
using StudioCRM.Domain.Entities;
using StudioCRM.Domain.Enums;
using StudioCRM.Infrastructure.Services;

namespace StudioCRM.Tests.IntegrationTests;

public class PublicGroupCrossLocationTests
{
    [PostgresFact]
    public async Task PersonalClientCanStartGroupPackageInAnotherLocation()
    {
        await using var database = await TestDatabase.CreateAsync();
        await using var db = database.Context();

        var user = new User { Email = "cross-location@example.test", EmailVerifiedAt = DateTime.UtcNow };
        var trainerUser = new User { Email = "trainer@example.test" };
        var home = new Location { Name = "Klaj", City = "Klaj" };
        var destinationCompany = new LegalEntity
        {
            Name = "Niepolomice company",
            TermsVersion = "2026-09",
            TermsUrl = "https://example.test/terms/2026-09"
        };
        var destination = new Location
        {
            Name = "Niepolomice",
            City = "Niepolomice",
            LegalEntity = destinationCompany
        };
        db.AddRange(user, trainerUser, home, destination);
        await db.SaveChangesAsync();

        var trainer = new Trainer { UserId = trainerUser.Id };
        db.Add(trainer);
        await db.SaveChangesAsync();

        var client = new Client
        {
            UserId = user.Id,
            LocationId = home.Id,
            FirstName = "Anna",
            LastName = "Nowak",
            Email = user.Email,
            TrainerId = trainer.Id
        };
        var package = new Package
        {
            Name = "Grupa 4 wejscia",
            Price = 200,
            SessionsLimit = 4,
            DurationDays = 30,
            BillingType = SessionBillingType.Group,
            LocationId = destination.Id,
            IsPubliclyAvailable = true
        };
        db.AddRange(client, package);
        await db.SaveChangesAsync();

        var service = new PublicGroupClassService(
            db,
            new ClientUser(user.Id),
            new NoOutlookSync(),
            NullLogger<PublicGroupClassService>.Instance);

        var requirements = await service.GetLegalRequirementsAsync(destination.Id);
        Assert.True(requirements.AcceptanceRequired);
        Assert.False(requirements.IsAccepted);
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.PurchasePackageForCurrentClientAsync(package.Id));

        await service.AcceptLegalTermsAsync(new StudioCRM.Application.DTOs.Public.AcceptPublicLegalTermsRequest
        {
            LocationId = destination.Id,
            AcceptTerms = true,
            TermsVersion = destinationCompany.TermsVersion
        });

        var purchase = await service.PurchasePackageForCurrentClientAsync(package.Id);

        var storedPackage = await db.ClientPackages.SingleAsync();
        var membership = await db.ClientLocationMemberships
            .SingleAsync(x => x.ClientId == client.Id && x.LocationId == destination.Id);

        Assert.Equal(home.Id, client.LocationId);
        Assert.Equal(destination.Id, storedPackage.LocationId);
        Assert.Equal(PaymentStatus.Unpaid, storedPackage.PaymentStatus);
        Assert.False(storedPackage.IsActive);
        Assert.Null(storedPackage.ValidUntil);
        Assert.True(membership.GroupAccessEnabled);
        Assert.False(membership.IsHomeLocation);
        Assert.Equal(storedPackage.Id, purchase.ClientPackageId);
        Assert.True(await db.Notifications.AnyAsync(x =>
            x.UserId == user.Id &&
            x.Type == "GroupPackagePaymentRequired" &&
            x.RelatedEntityId == storedPackage.Id));
    }

    private sealed class NoOutlookSync : IOutlookCalendarSyncService
    {
        public Task SyncSessionAsync(int sessionId) => Task.CompletedTask;
        public Task SyncSessionSeriesAsync(
            string recurringGroupId,
            StudioCRM.Application.DTOs.Sessions.SessionRecurrenceDto recurrence) => Task.CompletedTask;
        public Task DeleteSessionEventAsync(int sessionId) => Task.CompletedTask;
    }

    private sealed class ClientUser(int id) : ICurrentUserService
    {
        public int? UserId => id;
        public string? Email => null;
        public List<string> Roles => new() { "Client" };
        public bool IsAuthenticated => true;
        public bool IsOwner => false;
        public bool IsTrainer => false;
        public bool IsClient => true;
    }
}
