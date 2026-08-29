namespace StudioCRM.Application.DTOs.Billing;

public class UpdatePaymentProviderSettlementRequest
{
    public decimal? ProviderFeeAmount { get; set; }
    public decimal? ProviderNetAmount { get; set; }
    public DateTime? ProviderPayoutDate { get; set; }
    public DateTime? ProviderSettledAt { get; set; }
    public string? ProviderSettlementId { get; set; }
    public string? ProviderPaymentId { get; set; }
    public string? ProviderStatus { get; set; }
}
