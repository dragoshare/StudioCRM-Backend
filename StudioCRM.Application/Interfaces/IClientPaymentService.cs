using StudioCRM.Application.DTOs.Billing;

namespace StudioCRM.Application.Interfaces;

public interface IClientPaymentService
{
    Task<ClientBillingSummaryDto> GetCurrentClientSummaryAsync();
    Task<ClientBillingSummaryDto> GetClientSummaryAsync(int clientId);
    Task<List<ClientPaymentDto>> GetPendingConfirmationsAsync();
    Task<ClientPaymentDto> RequestPaymentAsClientAsync(CreateClientPaymentRequest request);
    Task<ClientPackagePurchaseResultDto> RequestPackageRenewalAsClientAsync(RequestClientPackageRenewalDto request);
    Task<ClientPaymentDto> CreatePaymentAsStaffAsync(CreateClientPaymentRequest request);
    Task<ClientPaymentDto> ConfirmAsync(int paymentId);
    Task<ClientPaymentDto> RejectAsync(int paymentId, RejectClientPaymentRequest request);
}
