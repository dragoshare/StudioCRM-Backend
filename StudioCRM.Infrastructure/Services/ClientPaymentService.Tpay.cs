using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using StudioCRM.Application.DTOs.Billing;
using StudioCRM.Domain.Entities;
using StudioCRM.Domain.Enums;

namespace StudioCRM.Infrastructure.Services;

public partial class ClientPaymentService
{
    public async Task<ClientPaymentDto> CreateTpayCheckoutAsync(int clientPackageId, CancellationToken ct)
    {
        // Production requires a separate rollout after sandbox settlement has been verified.
        if (!_tpaySettings.UseSandbox)
            throw new InvalidOperationException("Tpay checkout is currently available only in sandbox.");
        RequireHttpsUrl(_tpaySettings.BackendBaseUrl);
        RequireHttpsUrl(_tpaySettings.ReturnUrl);
        var client = await GetCurrentClientAsync();
        ClientPayment payment;
        string accountKey;
        string packageName;
        await using (var transaction = await _context.Database.BeginTransactionAsync(ct))
        {
            await LockTpayPackageAsync(clientPackageId, ct);
            var package = await _context.ClientPackages.Include(x => x.Location).ThenInclude(x => x!.LegalEntity)
                .SingleOrDefaultAsync(x => x.Id == clientPackageId && x.ClientId == client.Id, ct)
                ?? throw new InvalidOperationException("Client package not found.");
            var location = package.Location;
            if (location is null || !location.IsActive || location.LegalEntity is null || !location.LegalEntity.IsActive)
                throw new InvalidOperationException("Package location must have an active legal entity.");
            var accounts = await _context.PaymentProviderAccounts.Where(x => x.IsActive &&
                x.Provider.ToLower() == "tpay" && x.LegalEntityId == location.LegalEntityId &&
                (x.LocationId == null || x.LocationId == location.Id)).ToListAsync(ct);
            var candidates = accounts.Any(x => x.LocationId == location.Id)
                ? accounts.Where(x => x.LocationId == location.Id).ToList() : accounts;
            if (candidates.Count != 1)
                throw new InvalidOperationException("Exactly one active Tpay account must be configured for the package location.");
            var account = candidates[0];
            accountKey = account.AccountKey ?? string.Empty;
            RequireTpayAccount(accountKey);
            var amount = decimal.Round(package.TotalPrice - package.AmountPaid, 2);
            if (amount <= 0 || package.Currency != "PLN")
                throw new InvalidOperationException("Package must have a positive outstanding amount in PLN.");
            var pending = await _context.ClientPayments.Where(x => x.ClientPackageId == package.Id &&
                x.Status == ClientPaymentStatus.PendingConfirmation).OrderByDescending(x => x.Id).FirstOrDefaultAsync(ct);
            if (pending is not null)
            {
                if (pending.Source == ClientPaymentSource.PaymentGateway && pending.PaymentProvider == "Tpay" &&
                    pending.Amount == amount && pending.PaymentProviderAccountId == account.Id &&
                    !string.IsNullOrWhiteSpace(pending.CheckoutUrl))
                    return await GetPaymentDtoAsync(pending.Id);
                throw new InvalidOperationException("A payment is already pending. Reconcile it before creating another checkout.");
            }
            payment = new ClientPayment
            {
                ClientId = client.Id, ClientPackageId = package.Id, LocationId = location.Id,
                LegalEntityId = location.LegalEntityId, PaymentProviderAccountId = account.Id,
                PaymentProvider = "Tpay", Amount = amount, Currency = "PLN",
                Method = PaymentMethod.PaymentGateway, Source = ClientPaymentSource.PaymentGateway,
                Status = ClientPaymentStatus.PendingConfirmation, ProviderStatus = "sandbox:creating",
                CreatedByUserId = _currentUser.UserId,
                ReceiptRequired = location.FiscalReceiptMode != FiscalReceiptMode.NotRequired,
                ReceiptStatus = ReceiptStatus.None
            };
            packageName = package.Name;
            _context.ClientPayments.Add(payment);
            await _context.SaveChangesAsync(ct);
            await RefreshPackagePaymentStatusAsync(package);
            await _context.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
        }

        // Reserve locally before calling Tpay. An ambiguous network failure must not create a second charge.
        try
        {
            var result = await _tpay.CreateTransactionAsync(accountKey, payment.Amount, packageName,
                $"crm-payment-{payment.Id}", client.Email, $"{client.FirstName} {client.LastName}",
                _tpaySettings.BackendBaseUrl.TrimEnd('/') + "/api/payments/tpay/notifications",
                QueryHelpers.AddQueryString(_tpaySettings.ReturnUrl, "paymentId", payment.Id.ToString(CultureInfo.InvariantCulture)), ct);
            await using var transaction = await _context.Database.BeginTransactionAsync(ct);
            await LockTpayPackageAsync(clientPackageId, ct);
            await _context.Entry(payment).ReloadAsync(ct);
            if (payment.ExternalPaymentId is not null && payment.ExternalPaymentId != result.Title)
                throw new InvalidOperationException("Tpay transaction reference mismatch.");
            payment.ProviderPaymentId = result.TransactionId;
            payment.ExternalPaymentId = result.Title;
            payment.CheckoutUrl = result.CheckoutUrl;
            if (payment.Status == ClientPaymentStatus.PendingConfirmation)
                payment.ProviderStatus = "sandbox:pending";
            await _context.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
        }
        catch (HttpRequestException ex) when (ex.StatusCode is System.Net.HttpStatusCode.BadRequest or
            System.Net.HttpStatusCode.Unauthorized or System.Net.HttpStatusCode.Forbidden or System.Net.HttpStatusCode.UnprocessableEntity)
        {
            await using var transaction = await _context.Database.BeginTransactionAsync(CancellationToken.None);
            await LockTpayPackageAsync(clientPackageId, CancellationToken.None);
            await _context.Entry(payment).ReloadAsync(CancellationToken.None);
            if (payment.Status == ClientPaymentStatus.PendingConfirmation && payment.ExternalPaymentId is null)
            {
                payment.Status = ClientPaymentStatus.Rejected;
                payment.RejectedAt = DateTime.UtcNow;
                payment.ProviderStatus = "sandbox:create-rejected";
                await _context.SaveChangesAsync(CancellationToken.None);
                var package = await _context.ClientPackages.SingleAsync(x => x.Id == clientPackageId);
                await RefreshPackagePaymentStatusAsync(package);
                await _context.SaveChangesAsync(CancellationToken.None);
            }
            await transaction.CommitAsync(CancellationToken.None);
            throw new InvalidOperationException("Tpay rejected checkout creation. Check account configuration before retrying.");
        }
        catch (Exception ex) when (ex is HttpRequestException or OperationCanceledException or System.Text.Json.JsonException or KeyNotFoundException)
        {
            // Leave the durable reservation for callback recovery or manual reconciliation in Tpay.
            throw new InvalidOperationException("Checkout creation was not confirmed. Do not create another payment; check this payment in Tpay before retrying.");
        }
        return await GetPaymentDtoAsync(payment.Id);
    }

    public async Task<ClientPaymentDto> GetTpayPaymentAsync(int paymentId, CancellationToken ct)
    {
        var client = await GetCurrentClientAsync();
        if (!await _context.ClientPayments.AnyAsync(x => x.Id == paymentId && x.ClientId == client.Id &&
                x.Source == ClientPaymentSource.PaymentGateway && x.PaymentProvider == "Tpay", ct))
            throw new InvalidOperationException("Payment not found.");
        return await GetPaymentDtoAsync(paymentId);
    }

    public async Task HandleTpayNotificationAsync(byte[] body, string signature, CancellationToken ct)
    {
        if (!await _tpay.VerifySignatureAsync(body, signature, ct))
            throw new UnauthorizedAccessException("Invalid Tpay signature.");
        var form = QueryHelpers.ParseQuery(Encoding.UTF8.GetString(body));
        string Field(string key) => form.TryGetValue(key, out var values) && values.Count == 1
            ? values[0] ?? string.Empty : throw new InvalidOperationException("Missing or repeated notification field.");
        var reference = Field("tr_crc");
        const string prefix = "crm-payment-";
        if (!reference.StartsWith(prefix, StringComparison.Ordinal) ||
            !int.TryParse(reference[prefix.Length..], NumberStyles.None, CultureInfo.InvariantCulture, out var paymentId))
            throw new InvalidOperationException("Unknown payment reference.");
        var snapshot = await _context.ClientPayments.AsNoTracking().SingleOrDefaultAsync(x => x.Id == paymentId, ct)
            ?? throw new InvalidOperationException("Payment not found.");
        if (!snapshot.ClientPackageId.HasValue)
            throw new InvalidOperationException("Payment package not found.");
        await using var transaction = await _context.Database.BeginTransactionAsync(ct);
        await LockTpayPackageAsync(snapshot.ClientPackageId.Value, ct);
        var payment = await _context.ClientPayments.Include(x => x.ClientPackage).Include(x => x.PaymentProviderAccount)
            .SingleAsync(x => x.Id == paymentId, ct);
        var account = payment.PaymentProviderAccount;
        if (payment.Source != ClientPaymentSource.PaymentGateway || payment.PaymentProvider != "Tpay" ||
            account is null || account.LegalEntityId != payment.LegalEntityId)
            throw new InvalidOperationException("Payment provider mismatch.");
        var credentials = RequireTpayAccount(account.AccountKey ?? string.Empty);
        if (Field("id") != credentials.MerchantId || Field("test_mode") != "1" ||
            payment.ProviderStatus?.StartsWith("sandbox:", StringComparison.Ordinal) != true)
            throw new InvalidOperationException("Merchant or payment environment mismatch.");
        var title = Field("tr_id");
        if (string.IsNullOrWhiteSpace(title) || payment.ExternalPaymentId is not null && payment.ExternalPaymentId != title)
            throw new InvalidOperationException("Transaction title mismatch.");
        var amountText = Field("tr_amount");
        var checksum = Convert.ToHexString(MD5.HashData(Encoding.UTF8.GetBytes(
            Field("id") + title + amountText + reference + credentials.SecurityCode)));
        if (!CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(checksum),
                Encoding.ASCII.GetBytes(Field("md5sum").ToUpperInvariant())))
            throw new UnauthorizedAccessException("Invalid Tpay checksum.");
        if (!decimal.TryParse(amountText, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var amount) ||
            !decimal.TryParse(Field("tr_paid"), NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var paid) ||
            amount != payment.Amount || paid != payment.Amount || payment.Currency != "PLN" ||
            form.ContainsKey("tr_currency") && Field("tr_currency") != payment.Currency || Field("tr_error") != "none")
            throw new InvalidOperationException("Payment amount or currency mismatch. Manual review required.");
        if (Field("tr_status") != "true")
            throw new InvalidOperationException("Notification is not a successful payment. Refunds require manual review.");
        if (payment.Status is ClientPaymentStatus.Confirmed or ClientPaymentStatus.Reversed)
        {
            await transaction.CommitAsync(ct);
            return;
        }
        if (payment.Status != ClientPaymentStatus.PendingConfirmation)
            throw new InvalidOperationException("Payment requires manual reconciliation.");
        payment.ExternalPaymentId = title;
        payment.ProviderStatus = "sandbox:paid";
        payment.WebhookReceivedAt = DateTime.UtcNow;
        payment.Status = ClientPaymentStatus.Confirmed;
        payment.ConfirmedAt = DateTime.UtcNow;
        // Receipt handling uses the company/location captured at checkout, not a client's new location.
        var location = await _context.Locations.SingleAsync(x => x.Id == payment.LocationId, ct);
        payment.ReceiptStatus = payment.ReceiptRequired
            ? ResolveReceiptStatusAfterConfirmation(location.FiscalReceiptMode) : ReceiptStatus.None;
        await ApplyConfirmedPaymentAsync(payment, payment.ClientPackage);
        await _context.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
    }

    private StudioCRM.Application.Settings.TpayAccountSettings RequireTpayAccount(string key)
    {
        if (!_tpaySettings.Accounts.TryGetValue(key, out var account) ||
            string.IsNullOrWhiteSpace(account.ClientId) || string.IsNullOrWhiteSpace(account.ClientSecret) ||
            string.IsNullOrWhiteSpace(account.MerchantId))
            throw new InvalidOperationException("Tpay ClientId, ClientSecret and MerchantId must be configured for this account.");
        return account;
    }

    private Task LockTpayPackageAsync(int packageId, CancellationToken ct) =>
        _context.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(74121, {packageId})", ct);

    private async Task PrepareManualPackagePaymentAsync(ClientPackage? package)
    {
        if (package is null)
            return;
        await LockTpayPackageAsync(package.Id, default);
        await _context.Entry(package).ReloadAsync();
        if (await _context.ClientPayments.AnyAsync(x => x.ClientPackageId == package.Id &&
            x.Source == ClientPaymentSource.PaymentGateway && x.Status == ClientPaymentStatus.PendingConfirmation))
            throw new InvalidOperationException("A gateway payment is pending for this package. Reconcile it before entering another payment.");
    }

    private static void RequireHttpsUrl(string value)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) || uri.Scheme != "https" || uri.UserInfo.Length != 0 || uri.Fragment.Length != 0)
            throw new InvalidOperationException("Tpay BackendBaseUrl and ReturnUrl must be configured as absolute HTTPS URLs.");
    }
}
