using StudioCRM.Domain.Enums;

namespace StudioCRM.Application.DTOs.Billing;

public class RevenueAnalysisFilterDto
{
    public int? LocationId { get; set; }
    public int? LegalEntityId { get; set; }
    public int? TrainerId { get; set; }
    public int? ClientId { get; set; }
    public int? ClientPackageId { get; set; }
    public PaymentMethod? Method { get; set; }
    public string? PaymentProvider { get; set; }
    public bool? IsRenewal { get; set; }
    public bool? HasProviderFee { get; set; }
    public bool? IsProviderSettled { get; set; }
    public DateTime? From { get; set; }
    public DateTime? To { get; set; }
    public DateTime? PayoutFrom { get; set; }
    public DateTime? PayoutTo { get; set; }
}
