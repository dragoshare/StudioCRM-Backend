using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using StudioCRM.Application.ClientPackages.Services;
using StudioCRM.Application.DTOs.Settings;
using StudioCRM.Application.Interfaces;
using StudioCRM.Domain.Entities;
using StudioCRM.Domain.Enums;
using StudioCRM.Infrastructure.Persistence;
using StudioCRM.Infrastructure.Services;

namespace StudioCRM.Tests.IntegrationTests;
public class PackageFlowTests
{
    private sealed class Settings : IStudioSettingsService
    {
        public Task<OwnerSettingsDto> GetOwnerSettingsAsync() => Task.FromResult(new OwnerSettingsDto { DefaultPackageValidityDays = 90, DefaultPaymentDueDays = 7 });
        public Task<OwnerSettingsDto> UpdateOwnerSettingsAsync(UpdateOwnerSettingsDto r) => throw new NotSupportedException();
    }
    private static async Task<(Client client, Session session, Package catalog, PortalSecurityTests.Actor owner)> Seed(StudioCRMDbContext db)
    {
        var trainer = new Trainer { User = new User { Email = "owner-flow@test.invalid" } };
        var location = new Location { Name = "Flow" };
        var client = new Client { Location = location, Trainer = trainer, FirstName = "Test" };
        var session = new Session { Trainer = trainer, Location = location, PlannedSessionType = "Group", PublicCapacity = 2,
            StartAt = DateTime.UtcNow.AddDays(2), EndAt = DateTime.UtcNow.AddDays(2).AddHours(1) };
        var catalog = new Package { Name = "Group", Location = location, BillingType = SessionBillingType.Group, SessionsLimit = 4, DurationDays = 30, Price = 100, IsActive = true };
        db.AddRange(session, catalog, new ClientLocationMembership { Client = client, Location = location, GroupAccessEnabled = true });
        await db.SaveChangesAsync(); return (client, session, catalog, new(trainer.UserId, "Owner"));
    }
    private static SessionParticipantService Participants(StudioCRMDbContext db, PortalSecurityTests.Actor owner) =>
        new(db, owner, null!, new PortalSecurityTests.NoSync(), NullLogger<SessionParticipantService>.Instance);

    [PostgresFact]
    public async Task DiscountedSemiUsesFixedAdjustmentInBothAccountingPaths()
    {
        await using var database = await TestDatabase.CreateAsync(); await using var db = database.Context();
        var (client, session, catalog, owner) = await Seed(db);
        session.PlannedSessionType = "TwoToOne"; catalog.BillingType = SessionBillingType.TwoToOne;
        var pass = new ClientPackage { Client = client, Package = catalog, LocationId = session.LocationId,
            ExpectedBillingType = SessionBillingType.TwoToOne, TotalSessions = 8, ExpectedUnitPrice = 103.75m,
            AgreedSessionTermsJson = "[{\"BillingType\":2,\"Limit\":8,\"OpeningUsed\":0}]" };
        var participant = new SessionParticipant { Client = client, Session = session };
        db.AddRange(pass, participant); await db.SaveChangesAsync();
        await Participants(db, owner).CompleteSessionAsync(session.Id, new()
        { ActualSessionType = "OneToOne", Participants = [new() { ClientId = client.Id, AttendanceStatus = "Present", CountsAgainstPackage = true, SessionsCharged = 2 }] });
        Assert.Equal(-60m, participant.BalanceDifference);
        Assert.Equal(133.75m, participant.ActualUnitPrice);
        Assert.Equal(-60m, await db.ClientBalanceTransactions.SumAsync(t => t.Amount));
        await PortalSecurityTests.Sessions(db, owner).CountSessionFromPackageAsync(new()
        { SessionParticipantId = participant.Id, ClientPackageId = pass.Id, ActualBillingType = SessionBillingType.TwoToOne });
        Assert.Equal(0m, participant.BalanceDifference);
        Assert.Equal(103.75m, participant.ActualUnitPrice);
        Assert.Empty(await db.ClientBalanceTransactions.ToListAsync());
    }

    [PostgresFact]
    public async Task CancellingAndRecompletingSessionCountsTheEntryAgain()
    {
        await using var database = await TestDatabase.CreateAsync(); await using var db = database.Context();
        var (client, session, catalog, owner) = await Seed(db);
        session.PlannedSessionType = "TwoToOne"; catalog.BillingType = SessionBillingType.TwoToOne;
        var pass = new ClientPackage { Client = client, Package = catalog, LocationId = session.LocationId,
            ExpectedBillingType = SessionBillingType.TwoToOne, TotalSessions = 8, UsedSessions = 2, ExpectedUnitPrice = 60m };
        db.AddRange(pass, new SessionParticipant { Client = client, Session = session, CountsAgainstPackage = true });
        await db.SaveChangesAsync();
        var participants = Participants(db, owner);
        var sessions = new SessionService(db, owner, null!, participants, new PortalSecurityTests.NoSync(), null!,
            NullLogger<SessionService>.Instance);
        await participants.CompleteSessionAsync(session.Id, new()
        { ActualSessionType = "TwoToOne", Participants = [new() { ClientId = client.Id, AttendanceStatus = "Present", CountsAgainstPackage = true }] });
        Assert.Equal(3, pass.UsedSessions);

        StudioCRM.Application.DTOs.Sessions.UpdateSessionDto Edit(string status) => new()
        { StartAt = DateTime.SpecifyKind(session.StartAt, DateTimeKind.Utc), EndAt = DateTime.SpecifyKind(session.EndAt, DateTimeKind.Utc),
          TrainerId = session.TrainerId, LocationId = session.LocationId, PlannedSessionType = "TwoToOne", Status = status };

        await sessions.UpdateAsync(session.Id, Edit("Cancelled"));
        db.ChangeTracker.Clear();
        Assert.Equal(2, (await db.ClientPackages.SingleAsync(x => x.Id == pass.Id)).UsedSessions);
        // Cancelling reverts the accounting but keeps the declaration, so completing again counts the entry.
        Assert.True((await db.SessionParticipants.SingleAsync()).CountsAgainstPackage);

        await sessions.UpdateAsync(session.Id, Edit("Completed"));
        db.ChangeTracker.Clear();
        Assert.Equal(3, (await db.ClientPackages.SingleAsync(x => x.Id == pass.Id)).UsedSessions);
        Assert.True((await db.SessionParticipants.SingleAsync()).IsCountedFromPackage);
    }

    [PostgresFact]
    public async Task IndividualMixedAgreementPreservesOpeningUsageAndDoesNotCreateBalance()
    {
        await using var database = await TestDatabase.CreateAsync(); await using var db = database.Context();
        var (client, session, catalog, owner) = await Seed(db);
        session.PlannedSessionType = "TwoToOne";
        catalog.BillingType = SessionBillingType.OneToOne;
        var pass = new ClientPackage { Client = client, Package = catalog, LocationId = session.LocationId,
            ExpectedBillingType = SessionBillingType.OneToOne, TotalSessions = 12, UsedSessions = 1,
            ExpectedUnitPrice = 117.50m, TotalPrice = 1410m, OriginalPrice = 1410m,
            AgreedSessionTermsJson = "[{\"BillingType\":1,\"Limit\":6,\"OpeningUsed\":1},{\"BillingType\":2,\"Limit\":6,\"OpeningUsed\":0}]" };
        var participant = new SessionParticipant { Client = client, Session = session };
        db.AddRange(pass, participant); await db.SaveChangesAsync();
        var request = new StudioCRM.Application.DTOs.SessionParticipants.CompleteSessionDto
        { ActualSessionType = "TwoToOne", Participants = [new() { ClientId = client.Id, AttendanceStatus = "Present", CountsAgainstPackage = true }] };
        await Participants(db, owner).CompleteSessionAsync(session.Id, request);
        Assert.Equal(2, pass.UsedSessions);
        Assert.Equal(117.50m, participant.ActualUnitPrice);
        Assert.Equal(SessionBillingType.TwoToOne, participant.ActualBillingType);
        Assert.Empty(await db.ClientBalanceTransactions.ToListAsync());
        // Recount through the individual-session endpoint must not lose the opening session or double count.
        await PortalSecurityTests.Sessions(db, owner).CountSessionFromPackageAsync(new()
        { SessionParticipantId = participant.Id, ClientPackageId = pass.Id, ActualBillingType = SessionBillingType.TwoToOne });
        Assert.Equal(2, pass.UsedSessions);
        Assert.Empty(await db.ClientBalanceTransactions.ToListAsync());
    }

    [PostgresFact]
    public async Task ConsultationIsTrainerTimeWithoutClientsAndCompletesAutomatically()
    {
        await using var database = await TestDatabase.CreateAsync(); await using var db = database.Context();
        var (_, session, _, owner) = await Seed(db);
        session.PlannedSessionType = StudioCRM.Application.Common.ConsultationSessions.SessionType;
        session.Title = "Magdalena M";
        session.StartAt = DateTime.UtcNow.AddHours(-2); session.EndAt = DateTime.UtcNow.AddHours(-1);
        await db.SaveChangesAsync();

        var worker = new SessionAutoCompletionService(db, Participants(db, owner), NullLogger<SessionAutoCompletionService>.Instance);
        var result = await worker.CompleteFinishedSessionsAsync();

        Assert.Equal(1, result.CompletedCount);
        var saved = await db.Sessions.AsNoTracking().SingleAsync(s => s.Id == session.Id);
        Assert.Equal("Completed", saved.Status);
        Assert.Equal("Consultation", saved.ActualSessionType);
        Assert.Equal("Magdalena M", saved.Title);
        Assert.Empty(await db.SessionParticipants.ToListAsync());
        Assert.Empty(await db.ClientBalanceTransactions.ToListAsync());
    }

    [PostgresFact]
    public Task ImportedRunningPackageKeepsOpeningEntries() => AssertOpeningEntriesKept(null);

    [PostgresFact]
    public Task ImportedAgreedPackageKeepsOpeningEntries() =>
        AssertOpeningEntriesKept("[{\"BillingType\":2,\"Limit\":8,\"OpeningUsed\":0}]");

    // Regression: "Wprowadź trwający pakiet" stores 4/8 without session participants; the next session must give 5/8, not 1/8.
    private static async Task AssertOpeningEntriesKept(string? agreement)
    {
        await using var database = await TestDatabase.CreateAsync(); await using var db = database.Context();
        var (client, session, catalog, owner) = await Seed(db);
        session.PlannedSessionType = "TwoToOne"; catalog.BillingType = SessionBillingType.TwoToOne;
        var pass = new ClientPackage { Client = client, Package = catalog, LocationId = session.LocationId,
            ExpectedBillingType = SessionBillingType.TwoToOne, TotalSessions = 8, UsedSessions = 4, ExpectedUnitPrice = 115m,
            TotalPrice = 920m, AmountPaid = 920m, PaymentStatus = PaymentStatus.Paid, RenewalSource = "OpeningBalance",
            AgreedSessionTermsJson = agreement };
        var participant = new SessionParticipant { Client = client, Session = session };
        db.AddRange(pass, participant); await db.SaveChangesAsync();
        var request = new StudioCRM.Application.DTOs.SessionParticipants.CompleteSessionDto
        { ActualSessionType = "TwoToOne", Participants = [new() { ClientId = client.Id, AttendanceStatus = "Present", CountsAgainstPackage = true }] };
        await Participants(db, owner).CompleteSessionAsync(session.Id, request);
        Assert.Equal(5, pass.UsedSessions);
        // Completing again and recounting the same participant must neither lose opening entries nor double count.
        await Participants(db, owner).CompleteSessionAsync(session.Id, request);
        Assert.Equal(5, pass.UsedSessions);
        await PortalSecurityTests.Sessions(db, owner).CountSessionFromPackageAsync(new()
        { SessionParticipantId = participant.Id, ClientPackageId = pass.Id, ActualBillingType = SessionBillingType.TwoToOne });
        Assert.Equal(5, pass.UsedSessions);
        Assert.Equal(5, (await db.ClientPackages.AsNoTracking().SingleAsync(x => x.Id == pass.Id)).UsedSessions);
    }

    [PostgresFact]
    public async Task WorkerKeepsSingleParticipantGroupSeparateFromPersonalPackage()
    {
        await using var database = await TestDatabase.CreateAsync(); await using var db = database.Context();
        var (client, session, catalog, owner) = await Seed(db);
        session.StartAt = DateTime.UtcNow.AddHours(-2); session.EndAt = DateTime.UtcNow.AddHours(-1);
        var group = new ClientPackage { Client = client, Package = catalog, ExpectedBillingType = SessionBillingType.Group,
            PaymentStatus = PaymentStatus.Paid, TotalSessions = 4, ExpectedUnitPrice = 25, LocationId = session.LocationId };
        var personal = new ClientPackage { Client = client, Package = new Package { Name = "Personal", BillingType = SessionBillingType.FourToOne },
            ExpectedBillingType = SessionBillingType.FourToOne, TotalSessions = 8, ExpectedUnitPrice = 100 };
        db.AddRange(group, personal, new SessionParticipant { Session = session, Client = client }); await db.SaveChangesAsync();
        var worker = new SessionAutoCompletionService(db, Participants(db, owner), NullLogger<SessionAutoCompletionService>.Instance);
        var result = await worker.CompleteFinishedSessionsAsync();
        Assert.Equal(1, result.CompletedCount); Assert.Equal(0, result.FailedCount);
        Assert.Equal("Group", (await db.Sessions.SingleAsync()).ActualSessionType);
        Assert.Equal(1, group.UsedSessions); Assert.Equal(0, personal.UsedSessions);
        Assert.Equal(group.Id, (await db.SessionParticipants.SingleAsync()).ClientPackageId);
    }

    [PostgresFact]
    public async Task ManualAndCompletionPathsRejectGroupPersonalMixWithoutCharging()
    {
        await using var database = await TestDatabase.CreateAsync(); await using var db = database.Context();
        var (client, session, catalog, owner) = await Seed(db);
        var pass = new ClientPackage { Client = client, Package = catalog, ExpectedBillingType = SessionBillingType.OneToOne, TotalSessions = 4 };
        var participant = new SessionParticipant { Client = client, Session = session };
        db.AddRange(pass, participant); await db.SaveChangesAsync();
        await Assert.ThrowsAsync<InvalidOperationException>(() => PortalSecurityTests.Sessions(db, owner).CountSessionFromPackageAsync(new()
            { SessionParticipantId = participant.Id, ClientPackageId = pass.Id, ActualBillingType = SessionBillingType.Group }));
        await Assert.ThrowsAsync<InvalidOperationException>(() => Participants(db, owner).CompleteSessionAsync(session.Id, new()
            { ActualSessionType = "OneToOne", Participants = [new() { ClientId = client.Id, AttendanceStatus = "Present", CountsAgainstPackage = true }] }));
        Assert.Equal(0, await db.ClientPackages.Select(p => p.UsedSessions).SingleAsync());
        Assert.Empty(await db.ClientBalanceTransactions.ToListAsync());
    }

    [PostgresFact]
    public async Task WaitingForPaymentDoesNotReserveAndConfirmedPaymentAllowsBooking()
    {
        await using var database = await TestDatabase.CreateAsync(); await using var db = database.Context();
        var (client, session, catalog, owner) = await Seed(db);
        var service = new GroupEnrollmentService(db, owner, Participants(db, owner));
        var request = new StudioCRM.Application.DTOs.SessionParticipants.PrepareGroupEnrollmentRequest { ClientId = client.Id, PackageId = catalog.Id };
        var waiting = await service.PrepareAsync(session.Id, request);
        Assert.Equal("AwaitingPayment", waiting.Status); Assert.False(waiting.ReservesSeat);
        Assert.Equal(waiting.Id, (await service.PrepareAsync(session.Id, request)).Id);
        Assert.Single(await db.ClientPackages.ToListAsync()); Assert.Empty(await db.SessionParticipants.ToListAsync());
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.ConfirmAsync(waiting.Id));
        await PortalSecurityTests.Payments(db, owner).CreatePaymentAsStaffAsync(new() { ClientId = client.Id, ClientPackageId = waiting.ClientPackageId, Amount = 100, Method = PaymentMethod.Cash });
        Assert.Empty(await db.SessionParticipants.ToListAsync());
        var booked = await service.ConfirmAsync(waiting.Id);
        Assert.Equal("Booked", booked.Status); Assert.True(booked.ReservesSeat);
        Assert.Equal(booked, await service.ConfirmAsync(waiting.Id));
        Assert.Single(await db.SessionParticipants.ToListAsync());
        await Assert.ThrowsAsync<KeyNotFoundException>(() => new GroupEnrollmentService(db, new PortalSecurityTests.Actor(999, "Client"), Participants(db, owner)).GetAsync(waiting.Id));
    }

    [PostgresFact]
    public async Task PaymentDoesNotGuaranteeSeatWhenClassBecomesFull()
    {
        await using var database = await TestDatabase.CreateAsync(); await using var db = database.Context();
        var (client, session, catalog, owner) = await Seed(db);
        var service = new GroupEnrollmentService(db, owner, Participants(db, owner));
        var waiting = await service.PrepareAsync(session.Id, new() { ClientId = client.Id, PackageId = catalog.Id });
        await PortalSecurityTests.Payments(db, owner).CreatePaymentAsStaffAsync(new() { ClientId = client.Id, ClientPackageId = waiting.ClientPackageId, Amount = 100, Method = PaymentMethod.Cash });
        session.PublicCapacity = 0; await db.SaveChangesAsync();
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.ConfirmAsync(waiting.Id));
        Assert.Empty(await db.SessionParticipants.ToListAsync());
        Assert.Equal(PaymentStatus.Paid, (await db.ClientPackages.SingleAsync()).PaymentStatus);
    }

    [PostgresFact]
    public async Task ExpiredPackageBlocksSessionUntilOwnerExtendsIt()
    {
        await using var database = await TestDatabase.CreateAsync(); await using var db = database.Context();
        var (client, session, catalog, owner) = await Seed(db);
        var pass = new ClientPackage { Client = client, Package = catalog, LocationId = session.LocationId, ExpectedBillingType = SessionBillingType.Group,
            TotalSessions = 4, PaymentStatus = PaymentStatus.Paid, ValidUntil = DateTime.UtcNow.AddDays(-1), IsActive = true };
        db.Add(pass); await db.SaveChangesAsync();
        await Assert.ThrowsAsync<InvalidOperationException>(() => Participants(db, owner).AddParticipantAsync(session.Id, new() { ClientId = client.Id }));
        var management = new ClientPackageService(db, owner, new Settings());
        var preview = await management.GetManagementPreviewAsync(client.Id, pass.Id);
        await management.DecideExpiredAsync(client.Id, pass.Id, new() { Action = "Extend", ValidUntil = DateTime.UtcNow.AddDays(10), ExpectedVersion = preview.Version, Reason = "Owner approved" });
        await Participants(db, owner).AddParticipantAsync(session.Id, new() { ClientId = client.Id });
        Assert.Single(await db.SessionParticipants.ToListAsync());
        Assert.Contains(await db.ClientAuditEntries.ToListAsync(), x => x.Action == "ExpiredPackageExtended");
    }

    [Theory]
    [InlineData(true)] [InlineData(false)]
    public async Task OwnerEndsExpiredCycleAndRenewalHonorsClientSetting(bool autoRenew)
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("TPAY_TEST_DB"))) return;
        await using var database = await TestDatabase.CreateAsync(); await using var db = database.Context();
        var (client, session, catalog, owner) = await Seed(db);
        catalog.BillingType = SessionBillingType.OneToOne; client.SubscriptionAutoRenewEnabled = autoRenew;
        var pass = new ClientPackage { Client = client, Package = catalog, ExpectedBillingType = SessionBillingType.OneToOne,
            TotalSessions = 4, UsedSessions = 1, TotalPrice = 100, AmountPaid = 40, ValidUntil = DateTime.UtcNow.AddDays(-1), IsActive = true };
        db.Add(pass); await db.SaveChangesAsync();
        var service = new ClientPackageService(db, owner, new Settings());
        var preview = await service.GetManagementPreviewAsync(client.Id, pass.Id);
        var next = await service.DecideExpiredAsync(client.Id, pass.Id, new() { Action = "End", ExpectedVersion = preview.Version, Reason = "End expired cycle" });
        Assert.Equal(autoRenew, next.HasValue); Assert.False(pass.IsActive); Assert.Equal(1, pass.UsedSessions); Assert.Equal(40, pass.AmountPaid);
        Assert.Equal("ClosedWithDebt", pass.ClosureDisposition);
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => service.DecideExpiredAsync(client.Id, pass.Id, new() { Action = "End", ExpectedVersion = preview.Version, Reason = "Retry" }));
        Assert.Equal(autoRenew ? 2 : 1, await db.ClientPackages.CountAsync());
    }
}
