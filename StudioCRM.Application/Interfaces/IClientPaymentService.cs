using StudioCRM.Application.DTOs.Billing;

namespace StudioCRM.Application.Interfaces;

public interface IClientPaymentService
{
    Task<ClientBillingSummaryDto> GetCurrentClientSummaryAsync();
    Task<ClientBillingSummaryDto> GetClientSummaryAsync(int clientId);
    Task<PagedResultDto<ClientPaymentDto>> GetPaymentsAsync(ClientPaymentFilterDto filter);
    Task<PagedResultDto<ClientPaymentDto>> GetClientPaymentsAsync(int clientId, ClientPaymentFilterDto filter);
    Task<PagedResultDto<ClientBalanceTransactionDto>> GetClientBalanceTransactionsAsync(int clientId, int page, int pageSize);
    Task<ClientPackageBillingDto?> GetActivePackageAsync(int clientId);
    Task<List<ClientPaymentDto>> GetPendingConfirmationsAsync();
    Task<ClientPaymentDto> RequestPaymentAsClientAsync(CreateClientPaymentRequest request);
    Task<ClientPaymentDto> CreatePaymentAsStaffAsync(CreateClientPaymentRequest request);
    Task<ClientPaymentDto> ConfirmAsync(int paymentId);
    Task<ClientPaymentDto> RejectAsync(int paymentId, RejectClientPaymentRequest request);
    Task<ClientPaymentDto> IssueReceiptAsync(int paymentId, IssueReceiptRequest request);
    Task<ClientPaymentDto> CancelReceiptAsync(int paymentId);
    Task<ClientPaymentDto> ReverseAsync(int paymentId, ReverseClientPaymentRequest request);
}
