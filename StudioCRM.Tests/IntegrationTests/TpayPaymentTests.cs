using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using StudioCRM.Application.DTOs.Billing;
using StudioCRM.Application.Interfaces;
using StudioCRM.Application.Settings;
using StudioCRM.Domain.Entities;
using StudioCRM.Domain.Enums;
using StudioCRM.Infrastructure.Persistence;
using StudioCRM.Infrastructure.Services;

namespace StudioCRM.Tests.IntegrationTests;

public class TpayPaymentTests
{
    [PostgresFact]
    public async Task CheckoutAndConcurrentCallbacksAreIsolatedAndIdempotent()
    {
        await using var database = await TestDatabase.CreateAsync();
        await using var setup = database.Context();
        var company = new LegalEntity { Name = "Test company" };
        var location = new Location { Name = "Test location", LegalEntity = company };
        var user = new User { Email = "tpay-test@example.test" };
        var client = new Client { User = user, Location = location, Email = user.Email, FirstName = "Test" };
        var package = new ClientPackage
        {
            Client = client, Location = location, Package = new Package { Name = "Test package", Location = location },
            Name = "Four sessions", TotalPrice = 100, TotalSessions = 4, PurchaseDate = DateTime.UtcNow
        };
        setup.Add(package);
        setup.Add(new PaymentProviderAccount { LegalEntity = company, Provider = "Tpay", AccountKey = "Studio1", DisplayName = "Test" });
        await setup.SaveChangesAsync();
        var settings = Options.Create(new TpaySettings
        {
            BackendBaseUrl = "https://backend.example.test", ReturnUrl = "https://front.example.test/payment",
            Accounts = new() { ["Studio1"] = new() { ClientId = "id", ClientSecret = "secret", MerchantId = "123" } }
        });
        var api = new FakeTpay();
        ClientPaymentService Service(StudioCRMDbContext db, int? id = null) => new(db, new CurrentUser(id ?? user.Id), api, settings);

        await using var firstContext = database.Context();
        var checkoutTask = Service(firstContext).CreateTpayCheckoutAsync(package.Id, default);
        await api.Started.Task.WaitAsync(TimeSpan.FromSeconds(30));
        await using (var secondContext = database.Context())
            await Assert.ThrowsAsync<InvalidOperationException>(() => Service(secondContext).CreateTpayCheckoutAsync(package.Id, default));
        api.Release.TrySetResult();
        var checkout = await checkoutTask;
        Assert.Equal(100m, checkout.Amount);
        Assert.Equal(1, api.Calls);
        await using (var secondContext = database.Context())
        {
            var reused = await Service(secondContext).CreateTpayCheckoutAsync(package.Id, default);
            Assert.Equal(checkout.Id, reused.Id);
            await Assert.ThrowsAsync<InvalidOperationException>(() => Service(secondContext, user.Id + 1000).GetTpayPaymentAsync(checkout.Id, default));
            await Assert.ThrowsAsync<InvalidOperationException>(() => Service(secondContext).RequestPaymentAsClientAsync(
                new CreateClientPaymentRequest { ClientPackageId = package.Id, Amount = 100, Method = PaymentMethod.BankTransfer }));
        }
        var invalidBody = Notification(checkout.Id, "99.00");
        await using (var invalidContext = database.Context())
            await Assert.ThrowsAsync<InvalidOperationException>(() => Service(invalidContext).HandleTpayNotificationAsync(invalidBody, "test", default));
        var body = Notification(checkout.Id, "100.00");
        await Task.WhenAll(Enumerable.Range(0, 3).Select(async _ =>
        {
            await using var db = database.Context();
            await Service(db).HandleTpayNotificationAsync(body, "test", default);
        }));
        await using var verification = database.Context();
        var paidPackage = await verification.ClientPackages.SingleAsync();
        Assert.Equal(100, paidPackage.AmountPaid);
        Assert.Equal(PaymentStatus.Paid, paidPackage.PaymentStatus);
        var payment = await verification.ClientPayments.SingleAsync();
        Assert.Equal(ClientPaymentStatus.Confirmed, payment.Status);
        Assert.Equal(ReceiptStatus.ManualRequired, payment.ReceiptStatus);
        Assert.Equal(1, await verification.ClientBalanceTransactions.CountAsync());
        Assert.Equal(1, api.Calls);

        var retryPackage = new ClientPackage
        {
            Client = client, Location = location, Package = package.Package, Name = "Retry package",
            TotalPrice = 100, TotalSessions = 4, PurchaseDate = DateTime.UtcNow, IsActive = false,
            ExpectedBillingType = SessionBillingType.Group
        };
        setup.Add(retryPackage);
        await setup.SaveChangesAsync();
        api.Failure = new HttpRequestException("Rejected", null, System.Net.HttpStatusCode.BadRequest);
        await using (var rejectedContext = database.Context())
            await Assert.ThrowsAsync<InvalidOperationException>(() => Service(rejectedContext).CreateTpayCheckoutAsync(retryPackage.Id, default));
        Assert.Equal(ClientPaymentStatus.Rejected,
            await verification.ClientPayments.Where(x => x.ClientPackageId == retryPackage.Id).Select(x => x.Status).SingleAsync());

        api.Failure = new HttpRequestException("Connection lost after sending");
        await using (var ambiguousContext = database.Context())
            await Assert.ThrowsAsync<InvalidOperationException>(() => Service(ambiguousContext).CreateTpayCheckoutAsync(retryPackage.Id, default));
        var callCount = api.Calls;
        api.Failure = null;
        await using (var retryContext = database.Context())
            await Assert.ThrowsAsync<InvalidOperationException>(() => Service(retryContext).CreateTpayCheckoutAsync(retryPackage.Id, default));
        Assert.Equal(callCount, api.Calls);
        var pendingId = await verification.ClientPayments.Where(x => x.ClientPackageId == retryPackage.Id &&
            x.Status == ClientPaymentStatus.PendingConfirmation).Select(x => x.Id).SingleAsync();
        await using (var recoveryContext = database.Context())
            await Service(recoveryContext).HandleTpayNotificationAsync(Notification(pendingId, "100.00"), "test", default);
        Assert.Equal(100, await verification.ClientPackages.Where(x => x.Id == retryPackage.Id).Select(x => x.AmountPaid).SingleAsync());
        Assert.Equal(2, await verification.ClientPackages.CountAsync(x => x.IsActive));
    }

    private static byte[] Notification(int id, string amount)
    {
        var reference = $"crm-payment-{id}";
        var md5 = Convert.ToHexString(MD5.HashData(Encoding.UTF8.GetBytes("123TR-test" + amount + reference)));
        var query = QueryHelpers.AddQueryString("", new Dictionary<string, string?>
        {
            ["id"] = "123", ["tr_id"] = "TR-test", ["tr_crc"] = reference,
            ["tr_amount"] = amount, ["tr_paid"] = amount, ["tr_currency"] = "PLN",
            ["tr_status"] = "true", ["tr_error"] = "none", ["test_mode"] = "1", ["md5sum"] = md5
        });
        return Encoding.UTF8.GetBytes(query.TrimStart('?'));
    }

    private sealed class FakeTpay : ITpayApiClient
    {
        public int Calls;
        public Exception? Failure;
        public TaskCompletionSource Started = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public async Task<TpayTransaction> CreateTransactionAsync(string key, decimal amount, string description,
            string reference, string email, string name, string notificationUrl, string returnUrl, CancellationToken ct)
        {
            Interlocked.Increment(ref Calls);
            Assert.Equal("Studio1", key);
            Assert.Contains("/api/payments/tpay/notifications", notificationUrl);
            Started.TrySetResult();
            await Release.Task;
            if (Failure is not null)
                throw Failure;
            return new("transaction-id", "TR-test", "https://secure.sandbox.tpay.com/test");
        }
        public Task<bool> VerifySignatureAsync(byte[] body, string signature, CancellationToken ct) => Task.FromResult(true);
    }

    private sealed class CurrentUser(int id) : ICurrentUserService
    {
        public int? UserId => id;
        public string? Email => null;
        public List<string> Roles => new() { "Client" };
        public bool IsAuthenticated => true;
        public bool IsClient => true;
        public bool IsOwner => false;
        public bool IsTrainer => false;
    }
}
