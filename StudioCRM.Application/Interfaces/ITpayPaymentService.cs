using StudioCRM.Application.DTOs.Billing;

namespace StudioCRM.Application.Interfaces;

public interface ITpayPaymentService
{
    Task<ClientPaymentDto> CreateTpayCheckoutAsync(int clientPackageId, CancellationToken ct);
    Task<ClientPaymentDto> GetTpayPaymentAsync(int paymentId, CancellationToken ct);
    Task HandleTpayNotificationAsync(byte[] body, string signature, CancellationToken ct);
}
