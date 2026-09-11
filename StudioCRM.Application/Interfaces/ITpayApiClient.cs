namespace StudioCRM.Application.Interfaces;

public record TpayTransaction(string TransactionId, string Title, string CheckoutUrl);

public interface ITpayApiClient
{
    Task<TpayTransaction> CreateTransactionAsync(string accountKey, decimal amount, string description,
        string reference, string email, string name, string notificationUrl, string returnUrl, CancellationToken ct);
    Task<bool> VerifySignatureAsync(byte[] body, string signature, CancellationToken ct);
}
