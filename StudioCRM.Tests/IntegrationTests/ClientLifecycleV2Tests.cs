using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using StudioCRM.Application.DTOs.Clients;
using StudioCRM.Application.DTOs.Billing;
using StudioCRM.Application.Interfaces;
using StudioCRM.Application.Interfaces.Mail;
using StudioCRM.Application.Settings;
using StudioCRM.Domain.Entities;
using StudioCRM.Domain.Enums;
using StudioCRM.Infrastructure.Persistence;
using StudioCRM.Infrastructure.Services;

namespace StudioCRM.Tests.IntegrationTests;

public class ClientLifecycleV2Tests
{
    [PostgresFact]
    public async Task RetainArchiveReadHistoryRestoreAndResumePreserveUsageAndPayments()
    {
        await using var database = await TestDatabase.CreateAsync();
        await using var db = database.Context();
        var (client, package, staff) = await Seed(db);
        var service = new ClientService(db, staff, null!, null!);
        var paymentService = new ClientPaymentService(db, staff, null!, Options.Create(new TpaySettings()));
        var grossBefore = (await paymentService.GetRevenueStatisticsAsync(new())).GrossAmount;
        await service.CloseCooperationAsync(client.Id, Close(package, "Retain"));
        Assert.False(package.IsActive);
        Assert.Equal("Retained", package.ClosureDisposition);
        Assert.Equal(3, package.UsedSessions);
        Assert.False(client.SubscriptionAutoRenewEnabled);
        Assert.True(await service.DeleteAsync(client.Id));
        Assert.Equal("Blocked", (await service.GetByIdAsync(client.Id))!.PortalAccessStatus);
        var history = await service.GetPackageHistoryAsync(client.Id);
        Assert.Equal(package.Id, Assert.Single(history.Items).ClientPackageId);
        Assert.Equal(grossBefore, (await paymentService.GetRevenueStatisticsAsync(new())).GrossAmount);
        Assert.NotEmpty((await service.GetAuditAsync(client.Id)).Items);
        await service.RestoreAsync(client.Id);
        Assert.False(package.IsActive);
        await service.ResumeRetainedPackageAsync(client.Id, package.Id, new() { Reason = "Return", ValidUntil = DateTime.UtcNow.AddDays(30) });
        Assert.True(package.IsActive);
        Assert.Equal(3, package.UsedSessions);
        Assert.Equal(100m, package.AmountPaid);
        Assert.Null(package.ClosureDisposition);
        Assert.False(client.SubscriptionAutoRenewEnabled);
    }

    [PostgresFact]
    public async Task RefundNeedsExternalConfirmationIsIdempotentAndCannotResume()
    {
        await using var database = await TestDatabase.CreateAsync();
        await using var db = database.Context();
        var (client, package, staff) = await Seed(db);
        var service = new ClientService(db, staff, null!, null!);
        await service.CloseCooperationAsync(client.Id, Close(package, "Refund", 40));
        Assert.Contains("PendingRefunds", (await service.CheckArchiveAsync(client.Id)).Blockers);
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.ConfirmRefundAsync(client.Id, package.Id, new() { Amount = 39, Reference = "bank-1" }));
        var confirmation = new ConfirmClientRefundRequest { Amount = 40, Reference = "bank-1" };
        await service.ConfirmRefundAsync(client.Id, package.Id, confirmation);
        await service.ConfirmRefundAsync(client.Id, package.Id, confirmation);
        Assert.Equal(1, await db.ClientAuditEntries.CountAsync(x => x.Action == "RefundConfirmedExternally"));
        Assert.Equal(100m, package.AmountPaid);
        Assert.Equal("Refunded", Assert.Single((await service.GetRefundsAsync(client.Id)).Items).Disposition);
        Assert.DoesNotContain("PendingRefunds", (await service.CheckArchiveAsync(client.Id)).Blockers);
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.ResumeRetainedPackageAsync(client.Id, package.Id, new() { Reason = "No", ValidUntil = DateTime.UtcNow.AddDays(10) }));
    }

    [PostgresFact]
    public async Task ClosingWithSharedFutureSessionDoesNotMutateAnyParticipant()
    {
        await using var database = await TestDatabase.CreateAsync();
        await using var db = database.Context();
        var (client, package, staff) = await Seed(db);
        var other = new Client { LocationId = client.LocationId };
        var session = new Session { Trainer = new Trainer { User = new User { Email = "trainer@example.test" } }, LocationId = client.LocationId,
            StartAt = DateTime.UtcNow.AddDays(1), EndAt = DateTime.UtcNow.AddDays(1).AddHours(1), Status = "Planned" };
        db.AddRange(new SessionParticipant { Client = client, Session = session }, new SessionParticipant { Client = other, Session = session });
        await db.SaveChangesAsync();
        var service = new ClientService(db, staff, null!, null!);
        Assert.Contains(session.Id, (await service.GetClosurePreviewAsync(client.Id)).FutureSessionIds);
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.CloseCooperationAsync(client.Id, Close(package, "Retain")));
        Assert.True(package.IsActive);
        Assert.Equal("Planned", session.Status);
        Assert.Equal(2, await db.SessionParticipants.CountAsync());
    }

    [PostgresFact]
    public async Task LocationRemovalKeepsPastHistoryAndDuplicateSearchFindsArchivedClient()
    {
        await using var database = await TestDatabase.CreateAsync();
        await using var db = database.Context();
        var (client, package, staff) = await Seed(db);
        var second = new Location { Name = "Second" };
        db.Add(second);
        await db.SaveChangesAsync();
        var service = new ClientService(db, staff, null!, null!);
        await service.SetLocationAccessAsync(client.Id, second.Id, new() { GroupAccessEnabled = true, Reason = "Second studio" });
        Assert.Contains(await service.GetLocationsAsync(client.Id), x => x.LocationId == second.Id);
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.RemoveLocationAsync(client.Id, client.LocationId));
        await service.RemoveLocationAsync(client.Id, second.Id);
        Assert.DoesNotContain(await service.GetLocationsAsync(client.Id), x => x.LocationId == second.Id);
        await service.CloseCooperationAsync(client.Id, Close(package, "Retain"));
        await service.DeleteAsync(client.Id);
        var duplicate = Assert.Single(await service.FindDuplicatesAsync(new() { Email = " CLIENT@example.test " }));
        Assert.True(duplicate.IsArchived);
        Assert.Contains("Email", duplicate.Matches);
    }

    [PostgresFact]
    public async Task EmailApprovalRequiresProofRevokesRefreshAndLeavesContactEmailIntact()
    {
        await using var database = await TestDatabase.CreateAsync();
        await using var db = database.Context();
        var (client, _, staff) = await Seed(db);
        client.User = new User { Email = "old-login@example.test" };
        var change = new ClientEmailChangeRequest { Client = client, CurrentEmail = "old-login@example.test", RequestedEmail = "NEW@example.test" };
        var refresh = new RefreshToken { User = client.User, Token = "old-refresh", ExpiresAt = DateTime.UtcNow.AddDays(1) };
        db.AddRange(change, refresh);
        await db.SaveChangesAsync();
        var mail = new CaptureMail();
        var service = EmailService(db, staff, mail);
        await service.ReviewAsync(change.Id, new() { Approve = true });
        Assert.Equal("old-login@example.test", client.User.Email);
        Assert.Equal("AwaitingVerification", change.Status);
        Assert.NotNull(change.VerificationTokenHash);
        var token = mail.Link!.Split("&token=")[1];
        Assert.NotEqual(token, change.VerificationTokenHash);
        await service.VerifyAsync(new() { RequestId = change.Id, Token = token });
        Assert.Equal("new@example.test", client.User.Email);
        Assert.Equal("client@example.test", client.Email);
        Assert.NotNull(refresh.RevokedAt);
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.VerifyAsync(new() { RequestId = change.Id, Token = token }));
    }

    [PostgresFact]
    public async Task FailedEmailDeliveryCanRetryAndBlockingClientCancelsVerification()
    {
        await using var database = await TestDatabase.CreateAsync();
        await using var db = database.Context();
        var (client, _, staff) = await Seed(db);
        client.User = new User { Email = "old-login@example.test" };
        var change = new ClientEmailChangeRequest { Client = client, CurrentEmail = client.User.Email, RequestedEmail = "new@example.test" };
        db.Add(change);
        await db.SaveChangesAsync();
        var mail = new CaptureMail { Fail = true };
        var service = EmailService(db, staff, mail);
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.ReviewAsync(change.Id, new() { Approve = true }));
        await db.Entry(change).ReloadAsync();
        Assert.Equal("DeliveryFailed", change.Status);
        Assert.Null(change.VerificationTokenHash);
        mail.Fail = false;
        await service.ReviewAsync(change.Id, new() { Approve = true });
        var token = mail.Link!.Split("&token=")[1];
        await new ClientService(db, staff, null!, null!).SetPortalAccessAsync(client.Id, true);
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.VerifyAsync(new() { RequestId = change.Id, Token = token }));
        Assert.Equal("Cancelled", change.Status);
        Assert.Equal("old-login@example.test", client.User.Email);
    }

    [PostgresFact]
    public async Task ArchivedClientRemainsInCompletedSessionAndTrainerCannotReadArchivedProfile()
    {
        await using var database = await TestDatabase.CreateAsync();
        await using var db = database.Context();
        var (client, package, staff) = await Seed(db);
        var trainer = new Trainer { User = new User { Email = "trainer@example.test" } };
        var session = new Session { Trainer = trainer, LocationId = client.LocationId, StartAt = DateTime.UtcNow.AddDays(-2),
            EndAt = DateTime.UtcNow.AddDays(-2).AddHours(1), Status = "Completed" };
        client.Trainer = trainer;
        db.Add(new SessionParticipant { Client = client, Session = session });
        await db.SaveChangesAsync();
        var service = new ClientService(db, staff, null!, null!);
        await service.CloseCooperationAsync(client.Id, Close(package, "Retain"));
        await service.DeleteAsync(client.Id);
        db.ChangeTracker.Clear();
        var sessions = new SessionService(db, staff, null!, null!, null!, null!, Microsoft.Extensions.Logging.Abstractions.NullLogger<SessionService>.Instance);
        Assert.Equal(client.Id, Assert.Single((await sessions.GetByIdAsync(session.Id))!.Participants).ClientId);
        Assert.Equal(session.Id, Assert.Single((await service.GetSessionHistoryAsync(client.Id, new())).Items).SessionId);
        var trainerClientService = new ClientService(db, new Staff(trainer.UserId, false), null!, null!);
        Assert.Null(await trainerClientService.GetByIdAsync(client.Id));
    }

    private static CloseClientRequest Close(ClientPackage package, string disposition, decimal refund = 0) => new()
    { Reason = "End cooperation", Packages = new() { new() { ClientPackageId = package.Id, Disposition = disposition, RefundAmount = refund } } };

    private static ClientEmailChangeService EmailService(StudioCRMDbContext db, ICurrentUserService staff, IEmailService mail)
        => new(db, staff, mail, Options.Create(new AppSettings { FrontendBaseUrl = "https://crm.example.test" }));

    private static async Task<(Client, ClientPackage, Staff)> Seed(StudioCRMDbContext db)
    {
        var owner = new User { Email = "owner@example.test" };
        var client = new Client { FirstName = "Anna", LastName = "Nowak", Email = "client@example.test", Location = new Location { Name = "Studio" } };
        var package = new ClientPackage { Client = client, Package = new Package { Name = "10 sessions" }, Name = "Cycle", TotalSessions = 10,
            UsedSessions = 3, AmountPaid = 100, TotalPrice = 100, OriginalPrice = 100, PurchaseDate = DateTime.UtcNow, PaymentStatus = PaymentStatus.Paid };
        db.AddRange(owner, package, new ClientPayment { Client = client, ClientPackage = package, Amount = 100,
            Status = ClientPaymentStatus.Confirmed, PaymentDate = DateTime.UtcNow });
        await db.SaveChangesAsync();
        return (client, package, new Staff(owner.Id));
    }
    internal sealed class Staff(int id, bool owner = true) : ICurrentUserService
    {
        public int? UserId => id;
        public string? Email => null;
        public List<string> Roles => new() { owner ? "Owner" : "Trainer" };
        public bool IsAuthenticated => true;
        public bool IsOwner => owner;
        public bool IsTrainer => !owner;
        public bool IsClient => false;
    }
    private sealed class CaptureMail : IEmailService
    {
        public string? Link { get; private set; }
        public bool Fail { get; set; }
        public Task SendLoginEmailChangeVerificationAsync(string toEmail, string link)
        { if (Fail) throw new InvalidOperationException("Simulated transport failure"); Link = link; return Task.CompletedTask; }
        public Task SendInvitationEmailAsync(string a, string b, string c, string d) => throw new NotSupportedException();
        public Task SendPasswordResetEmailAsync(string a, string b) => throw new NotSupportedException();
        public Task SendEmailVerificationAsync(string a, string b) => throw new NotSupportedException();
    }
}
