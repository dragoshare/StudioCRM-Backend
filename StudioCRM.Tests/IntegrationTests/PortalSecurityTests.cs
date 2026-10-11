using System.Net;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using StudioCRM.Application.DTOs.Auth;
using StudioCRM.Application.DTOs.Billing;
using StudioCRM.Application.DTOs.Clients;
using StudioCRM.Application.DTOs.SessionParticipants;
using StudioCRM.Application.DTOs.Sessions;
using StudioCRM.Application.DTOs.TrainerPortal;
using StudioCRM.Application.Interfaces;
using StudioCRM.Application.Interfaces.Calendar;
using StudioCRM.Application.Settings;
using StudioCRM.Domain.Entities;
using StudioCRM.Domain.Enums;
using UserRole = StudioCRM.Domain.Entities.UserRole;
using StudioCRM.Infrastructure.Persistence;
using StudioCRM.Infrastructure.Services;
using StudioCRM.Infrastructure.Services.Calendar;

namespace StudioCRM.Tests.IntegrationTests;

public class PortalSecurityTests
{
    internal sealed class Actor(int id, string role = "Trainer") : ICurrentUserService
    {
        public int? UserId => id;
        public string? Email => null;
        public List<string> Roles => [role];
        public bool IsAuthenticated => true;
        public bool IsOwner => role == "Owner";
        public bool IsTrainer => role == "Trainer";
        public bool IsClient => role == "Client";
    }
    internal sealed class NoSync : IOutlookCalendarSyncService
    {
        public Task SyncSessionAsync(int id) => Task.CompletedTask;
        public Task DeleteSessionEventAsync(int id) => Task.CompletedTask;
        public Task<bool> DeleteSessionSeriesAsync(string id) => Task.FromResult(true);
        public Task SyncSessionSeriesAsync(string id, SessionRecurrenceDto recurrence) => Task.CompletedTask;
    }
    internal static SessionService Sessions(StudioCRMDbContext db, ICurrentUserService actor) =>
        new(db, actor, null!, null!, new NoSync(), null!, NullLogger<SessionService>.Instance);
    internal static ClientPaymentService Payments(StudioCRMDbContext db, ICurrentUserService actor) =>
        new(db, actor, null!, Options.Create(new TpaySettings()));
    internal static AuthService Auth(StudioCRMDbContext db) => new(db,
        Options.Create(new JwtSettings { Key = new string('k', 64), Issuer = "atlas-test", Audience = "atlas-test", ExpiryInMinutes = 15 }),
        Options.Create(new AppSettings()), null!, NullLogger<AuthService>.Instance);

    [PostgresFact]
    public async Task ReadsRespectTrainerLocationAndClientScopeAndHideOtherClientsFinancialData()
    {
        await using var database = await TestDatabase.CreateAsync();
        await using var db = database.Context();
        var a = new Location { Name = "Allowed", City = "Test" };
        var b = new Location { Name = "Forbidden", City = "Test" };
        var trainer = new Trainer { User = new User { Email = "trainer@test.invalid" } };
        var other = new Trainer { User = new User { Email = "other@test.invalid" } };
        var own = new Client { FirstName = "Own", LastName = "Client", Email = "own@test.invalid", Trainer = trainer, Location = a };
        var group = new Client { FirstName = "Group", LastName = "Client", Email = "group@test.invalid", Trainer = other, Location = a };
        var stranger = new Client { FirstName = "Foreign", LastName = "Client", Email = "foreign@test.invalid", Trainer = other, Location = b };
        var allowed = new Package { Name = "Semi", Location = a, BillingType = SessionBillingType.FourToOne };
        var forbidden = new Package { Name = "Private", Location = b };
        var global = new Package { Name = "Global" };
        var now = new DateTime(2026, 10, 10, 10, 0, 0, DateTimeKind.Utc);
        var sharedSession = new Session { Trainer = other, Location = a, StartAt = now, EndAt = now.AddHours(1), Note = "Private note" };
        var foreignSession = new Session { Trainer = other, Location = b, StartAt = now, EndAt = now.AddHours(1) };
        db.AddRange(new TrainerLocation { Trainer = trainer, Location = a }, own, group, stranger, allowed, forbidden, global,
            new ClientLocationMembership { Client = group, Location = a, GroupAccessEnabled = true },
            new ClientLocationMembership { Client = stranger, Location = b, GroupAccessEnabled = true },
            new SessionParticipant { Session = sharedSession, Client = group, Package = allowed, ExpectedUnitPrice = 123, Note = "Private participant note" }, foreignSession);
        await db.SaveChangesAsync();
        var user = new Actor(trainer.UserId);
        var clients = new ClientService(db, user, null!, null!);
        var sessions = Sessions(db, user);
        var portal = new TrainerPortalService(db, user, clients, sessions, null!);
        var page = await portal.GetClientsPageAsync(new() { PageSize = 1, Search = "own" });
        Assert.Equal(1, page.TotalCount);
        Assert.Equal(own.Id, Assert.Single(page.Items).ClientId);
        Assert.Empty((await portal.GetClientsPageAsync(new() { Search = "foreign" })).Items);
        Assert.Null(await clients.GetByIdAsync(stranger.Id));
        await Assert.ThrowsAsync<KeyNotFoundException>(() => clients.GetSessionHistoryAsync(group.Id, new()));
        Assert.Equal(group.Id, Assert.Single((await portal.GetGroupClientsAsync(new() { LocationId = a.Id })).Items).ClientId);
        await Assert.ThrowsAsync<KeyNotFoundException>(() => portal.GetGroupClientsAsync(new() { LocationId = b.Id }));
        Assert.Single(await portal.GetSessionsAsync(now.AddDays(-1), now.AddDays(1)));
        Assert.Empty(await portal.GetSessionsAsync(now.AddDays(2), now.AddDays(3)));
        await Assert.ThrowsAsync<InvalidOperationException>(() => portal.GetSessionsAsync(now, now.AddDays(94)));
        await Assert.ThrowsAsync<InvalidOperationException>(() => portal.GetSessionsAsync(now));
        Assert.Null(await sessions.GetByIdAsync(foreignSession.Id));
        var visible = Assert.Single(await sessions.GetFilteredAsync(new SessionFilterDto { LocationId = a.Id }));
        Assert.Null(visible.Note);
        var participant = Assert.Single(visible.Participants);
        Assert.Null(participant.Note);
        Assert.Null(participant.PackageId);
        Assert.Equal(0, participant.ExpectedUnitPrice);
        var ownerSession = await Sessions(db, new Actor(trainer.UserId, "Owner")).GetByIdAsync(sharedSession.Id);
        Assert.Equal(123, Assert.Single(ownerSession!.Participants).ExpectedUnitPrice);
        var packages = new PackageService(db, user);
        Assert.Null(await packages.GetByIdAsync(forbidden.Id));
        Assert.Equal(2, (await packages.GetOptionsAsync(new() { LocationId = a.Id })).TotalCount);
        Assert.Equal("SemiPersonal", (await packages.GetOptionsAsync(new() { Search = "Semi" })).Items.Single().PackageType);
    }

    [PostgresFact]
    public async Task BillingSummaryMatchesLegacyTotalsAndPendingPaymentsAreScoped()
    {
        await using var database = await TestDatabase.CreateAsync();
        await using var db = database.Context();
        var location = new Location { Name = "Billing", City = "Test" };
        var trainer = new Trainer { User = new User { Email = "trainer@test.invalid" } };
        var own = new Client { FirstName = "Own", LastName = "Client", Trainer = trainer, Location = location };
        var other = new Client { FirstName = "Other", LastName = "Client", Location = location };
        var package = new Package { Name = "Package", Location = location };
        var cycle = new ClientPackage { Client = own, Package = package, Name = "Cycle", TotalPrice = 400, AmountPaid = 100, TotalSessions = 4, IsActive = true };
        db.AddRange(cycle, new ClientPayment { Client = own, Amount = 80, Status = ClientPaymentStatus.PendingConfirmation, Source = ClientPaymentSource.ClientRequest },
            new ClientPayment { Client = other, Amount = 90, Status = ClientPaymentStatus.PendingConfirmation, Source = ClientPaymentSource.ClientRequest },
            new ClientBalanceTransaction { Client = own, Amount = 30, Type = BalanceTransactionType.PackageAdjustment });
        await db.SaveChangesAsync();
        var service = Payments(db, new Actor(trainer.UserId));
        var old = await service.GetClientSummaryAsync(own.Id);
        var summary = await service.GetClientTotalsAsync(own.Id);
        Assert.Equal(old.TotalAmountDue, summary.TotalAmountDue);
        Assert.Equal(old.CurrentBalance, summary.CurrentBalance);
        Assert.Equal(old.ActiveClientPackageId, summary.ActiveClientPackageId);
        var pending = await service.GetPendingPageAsync(new() { PageSize = 1, Search = "own" });
        Assert.Equal(1, pending.TotalCount);
        Assert.True(Assert.Single(pending.Items).CanConfirm);
        Assert.Empty((await service.GetPendingPageAsync(new() { Search = "other" })).Items);
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.GetClientTotalsAsync(other.Id));
    }

    [PostgresFact]
    public async Task ParallelConfirmOfStandalonePaymentCreditsBalanceOnce()
    {
        await using var database = await TestDatabase.CreateAsync();
        await using var seed = database.Context();
        var actor = new User { Email = "owner@test.invalid" };
        var client = new Client { FirstName = "Client", Location = new Location { Name = "A", City = "Test" } };
        var payment = new ClientPayment { Client = client, Amount = 60, Status = ClientPaymentStatus.PendingConfirmation, Source = ClientPaymentSource.ClientRequest };
        seed.AddRange(actor, payment); await seed.SaveChangesAsync();
        async Task<bool> Confirm()
        {
            await using var db = database.Context();
            try { await Payments(db, new Actor(actor.Id, "Owner")).ConfirmAsync(payment.Id); return true; }
            catch (InvalidOperationException) { return false; }
        }
        var results = await Task.WhenAll(Confirm(), Confirm());
        Assert.Single(results.Where(x => x));
        await using var check = database.Context();
        Assert.Equal(60, await check.ClientBalanceTransactions.Where(t => t.ClientId == client.Id && t.Type == BalanceTransactionType.PaymentOverpayment).SumAsync(t => t.Amount));
        Assert.Equal(1, await check.ClientBalanceTransactions.CountAsync(t => t.Type == BalanceTransactionType.PaymentCredit));
    }

    [PostgresFact]
    public async Task ParallelPaymentRetryWithSameKeyCreatesExactlyOnePaymentAndRejectsChangedBody()
    {
        await using var database = await TestDatabase.CreateAsync();
        await using var seed = database.Context();
        var user = new User { Email = "owner@test.invalid" };
        var client = new Client { FirstName = "Client", Location = new Location { Name = "A", City = "Test" } };
        seed.AddRange(user, client); await seed.SaveChangesAsync();
        var key = Guid.NewGuid();
        async Task<ClientPaymentDto> Create(decimal amount)
        {
            await using var db = database.Context();
            return await Payments(db, new Actor(user.Id, "Owner")).CreatePaymentAsStaffAsync(new()
            { ClientId = client.Id, Amount = amount, Method = PaymentMethod.Cash, IdempotencyKey = key });
        }
        var results = await Task.WhenAll(Create(50), Create(50));
        Assert.Equal(results[0].Id, results[1].Id);
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => Create(70));
        Assert.Equal(1, await seed.ClientPayments.CountAsync());
    }

    [PostgresFact]
    public async Task RefreshTokenIsSingleUseAndLogoutInvalidatesAccessVersionAndAllRefreshTokens()
    {
        await using var database = await TestDatabase.CreateAsync();
        await using var seed = database.Context();
        var user = new User { Email = "trainer@test.invalid", IsActive = true };
        seed.Add(new RefreshToken { User = user, Token = "test-refresh", ExpiresAt = DateTime.UtcNow.AddHours(1) });
        seed.Add(new UserRole { User = user, Role = new Role { Name = "Trainer" } });
        await seed.SaveChangesAsync();
        async Task<string?> Refresh()
        {
            await using var db = database.Context();
            try { return (await Auth(db).RefreshAsync(new() { RefreshToken = "test-refresh" })).RefreshToken; }
            catch (InvalidOperationException) { return null; }
        }
        var results = await Task.WhenAll(Refresh(), Refresh());
        var token = Assert.Single(results.Where(x => x != null));
        await using var verify = database.Context();
        var roles = new[] { "Trainer" };
        Assert.True(await verify.Users.AnyAsync(u => u.Id == user.Id && u.AuthVersion == 0 && roles.All(r => u.UserRoles.Any(ur => ur.Role.Name == r))));
        roles = ["Owner"];
        Assert.False(await verify.Users.AnyAsync(u => u.Id == user.Id && roles.All(r => u.UserRoles.Any(ur => ur.Role.Name == r))));
        await Auth(verify).LogoutAsync(user.Id);
        Assert.Equal(1, await verify.Users.Where(u => u.Id == user.Id).Select(u => u.AuthVersion).SingleAsync());
        await Assert.ThrowsAsync<InvalidOperationException>(() => Auth(verify).RefreshAsync(new() { RefreshToken = token! }));
        Assert.False(await verify.RefreshTokens.AnyAsync(t => t.UserId == user.Id && t.RevokedAt == null));
    }

    [PostgresFact]
    public async Task OAuthStateRequiresSameBrowserAndIsSingleUseWithPkce()
    {
        await using var database = await TestDatabase.CreateAsync();
        await using var db = database.Context();
        var user = new User { Email = "oauth@test.invalid", IsActive = true };
        db.Add(new UserRole { User = user, Role = new Role { Name = "Trainer" } }); await db.SaveChangesAsync();
        var browser = new DefaultHttpContext();
        var accessor = new HttpContextAccessor { HttpContext = browser };
        var handler = new OAuthHandler();
        var service = new OutlookCalendarAuthService(db, new Actor(user.Id), Options.Create(new OutlookSettings
        { TenantId = "common", ClientId = "test", RedirectUri = "https://api.test.invalid/api/outlook/callback", Scopes = "User.Read" }), new HttpClient(handler), accessor);
        var url = (await service.GetConnectUrlAsync()).Url;
        var query = QueryHelpers.ParseQuery(new Uri(url).Query);
        Assert.Equal("S256", query["code_challenge_method"]);
        var state = query["state"].ToString();
        Assert.Equal(43, state.Length);
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.ConnectCallbackAsync("code", user.Id.ToString()));
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.ConnectCallbackAsync("code", state));
        Assert.Equal(0, handler.Calls);
        browser.Request.Headers.Cookie = browser.Response.Headers.SetCookie.ToString().Split(';')[0];
        await service.ConnectCallbackAsync("code", state);
        Assert.True(handler.ReceivedVerifier);
        Assert.Equal(2, handler.Calls);
        Assert.Equal(user.Id, (await db.CalendarIntegrations.SingleAsync()).UserId);
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.ConnectCallbackAsync("code", state));
        Assert.Equal(2, handler.Calls);
    }

    private sealed class OAuthHandler : HttpMessageHandler
    {
        public int Calls { get; private set; }
        public bool ReceivedVerifier { get; private set; }
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Calls++;
            if (request.Method == HttpMethod.Post)
            {
                ReceivedVerifier = (await request.Content!.ReadAsStringAsync(ct)).Contains("code_verifier=");
                return new(HttpStatusCode.OK) { Content = new StringContent("{\"token_type\":\"Bearer\",\"scope\":\"User.Read\",\"access_token\":\"test-token\",\"refresh_token\":\"test-refresh\",\"expires_in\":3600}") };
            }
            return new(HttpStatusCode.OK) { Content = new StringContent("{\"id\":\"ms-id\",\"mail\":\"outlook@test.invalid\",\"userPrincipalName\":\"outlook@test.invalid\"}") };
        }
    }

    [PostgresFact]
    public async Task WebhookRejectsMissingWrongAndLegacyClientStateBeforeChangingEvents()
    {
        await using var database = await TestDatabase.CreateAsync();
        await using var db = database.Context();
        var integration = new CalendarIntegration { User = new User { Email = "webhook@test.invalid" } };
        var subscription = new CalendarSubscription { CalendarIntegration = integration, SubscriptionId = "test-subscription", ClientState = new string('a', 64), ExpiresAt = DateTime.UtcNow.AddDays(1) };
        var evt = new ExternalCalendarEvent { CalendarIntegration = integration, ExternalEventId = "event1", Subject = "Original" };
        db.AddRange(subscription, evt); await db.SaveChangesAsync();
        var service = new OutlookWebhookService(db, new HttpClient(), null!, null!, null!, null!);
        string Body(string? secret) => JsonSerializer.Serialize(new { value = new[] { new { subscriptionId = subscription.SubscriptionId, clientState = secret, resource = "me/events/event1", changeType = "deleted" } } });
        await service.HandleNotificationAsync(Body(null));
        await service.HandleNotificationAsync(Body("StudioCRM-Outlook-Webhook"));
        await service.HandleNotificationAsync(Body(new string('b', 64)));
        Assert.Equal("Original", evt.Subject);
        await service.HandleNotificationAsync(Body(subscription.ClientState));
        Assert.Equal("[DELETED] Original", evt.Subject);
    }

    [PostgresFact]
    public async Task OutlookIssueIdsCannotChangeAnotherTrainersClientOrWarnings()
    {
        await using var database = await TestDatabase.CreateAsync();
        await using var db = database.Context();
        var trainer = new Trainer { User = new User { Email = "actor@test.invalid" } };
        var other = new Trainer { User = new User { Email = "other@test.invalid" } };
        var client = new Client { Trainer = other, Location = new Location { Name = "A", City = "Test" }, Email = "original@test.invalid" };
        var evt = new ExternalCalendarEvent { CalendarIntegration = new CalendarIntegration { User = other.User }, ExternalEventId = "event", MappingWarningsJson = "[\"warning\"]" };
        db.AddRange(trainer, client, evt); await db.SaveChangesAsync();
        var service = new ExternalCalendarEventService(db, new Actor(trainer.UserId), null!);
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.LinkClientFromIssueAsync(client.Id, "changed@test.invalid"));
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.IgnoreIssueAsync(evt.Id, "warning"));
        Assert.Equal("original@test.invalid", client.Email);
        Assert.Equal("[\"warning\"]", evt.MappingWarningsJson);
    }

    [PostgresFact]
    public async Task ParallelAutomaticCompletionChargesOnce()
    {
        await using var database = await TestDatabase.CreateAsync();
        await using var seed = database.Context();
        var trainer = new Trainer { User = new User { Email = "completion@test.invalid" } };
        var location = new Location { Name = "A", City = "Test" };
        var client = new Client { Trainer = trainer, Location = location };
        var package = new Package { Name = "Group", BillingType = SessionBillingType.Group, Location = location, Price = 160, SessionsLimit = 4 };
        var cycle = new ClientPackage { Client = client, Package = package, Location = location, IsActive = true, ExpectedBillingType = SessionBillingType.Group, ExpectedUnitPrice = 40, TotalSessions = 4, TotalPrice = 160 };
        var session = new Session { Trainer = trainer, Location = location, PlannedSessionType = "Group", StartAt = DateTime.UtcNow.AddDays(-1), EndAt = DateTime.UtcNow.AddDays(-1).AddHours(1) };
        seed.AddRange(cycle, new SessionParticipant { Session = session, Client = client, ClientPackage = cycle }); await seed.SaveChangesAsync();
        async Task Complete()
        {
            await using var db = database.Context();
            var service = new SessionParticipantService(db, new Actor(trainer.UserId), null!, new NoSync(), NullLogger<SessionParticipantService>.Instance);
            Assert.True(await service.CompleteSessionAutomaticallyAsync(session.Id, new() { ActualSessionType = "Group", Participants = [new() { ClientId = client.Id, AttendanceStatus = "Present", CountsAgainstPackage = true, SessionsCharged = 1 }] }));
        }
        await Task.WhenAll(Complete(), Complete());
        await using var check = database.Context();
        Assert.Equal(1, await check.ClientPackages.Where(p => p.Id == cycle.Id).Select(p => p.UsedSessions).SingleAsync());
        Assert.Single(await check.SessionParticipants.Where(p => p.IsCountedFromPackage).ToListAsync());
        Assert.Empty(await check.SessionCorrections.ToListAsync());
    }

    [PostgresFact]
    public async Task StaffReservationsCompeteForTheSameCapacity()
    {
        await using var database = await TestDatabase.CreateAsync();
        await using var seed = database.Context();
        var trainer = new Trainer { User = new User { Email = "trainer@test.invalid" } };
        var location = new Location { Name = "Capacity", City = "Test" };
        var session = new Session { Trainer = trainer, Location = location, PlannedSessionType = "Group", PublicCapacity = 1,
            StartAt = DateTime.UtcNow.AddDays(3), EndAt = DateTime.UtcNow.AddDays(3).AddHours(1) };
        var clients = new[] { new Client { Location = location, FirstName = "A" }, new Client { Location = location, FirstName = "B" } };
        seed.AddRange(session, clients[0], clients[1]); await seed.SaveChangesAsync();
        async Task<bool> Add(int clientId)
        {
            await using var db = database.Context();
            var service = new SessionParticipantService(db, new Actor(trainer.UserId, "Owner"), null!, new NoSync(), NullLogger<SessionParticipantService>.Instance);
            try { await service.AddParticipantAsync(session.Id, new() { ClientId = clientId, CountsAgainstPackage = false }); return true; }
            catch (InvalidOperationException) { return false; }
        }
        Assert.Single((await Task.WhenAll(Add(clients[0].Id), Add(clients[1].Id))).Where(x => x));
        Assert.Equal(1, await seed.SessionParticipants.CountAsync());
    }
}
