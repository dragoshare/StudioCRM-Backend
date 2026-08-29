namespace StudioCRM.Application.DTOs.Billing;

public class RevenueStatisticsDto
{
    public DateTime? From { get; set; }
    public DateTime? To { get; set; }
    public DateTime? PayoutFrom { get; set; }
    public DateTime? PayoutTo { get; set; }

    public int PaymentCount { get; set; }
    public decimal GrossAmount { get; set; }
    public decimal ProviderFeeAmount { get; set; }
    public decimal NetAmount { get; set; }
    public decimal AppliedToPackageAmount { get; set; }
    public decimal BalanceCreditAmount { get; set; }
    public decimal NewPaymentGrossAmount { get; set; }
    public decimal RenewalPaymentGrossAmount { get; set; }
    public decimal WithoutPackageGrossAmount { get; set; }

    public List<RevenueBreakdownDto> ByLocation { get; set; } = new();
    public List<RevenueBreakdownDto> ByLegalEntity { get; set; } = new();
    public List<RevenueBreakdownDto> ByTrainer { get; set; } = new();
    public List<RevenueBreakdownDto> ByPackageType { get; set; } = new();
    public List<RevenueBreakdownDto> ByPackage { get; set; } = new();
    public List<RevenueBreakdownDto> ByClient { get; set; } = new();
    public List<RevenueBreakdownDto> ByPaymentMethod { get; set; } = new();
    public List<RevenueBreakdownDto> ByPaymentProvider { get; set; } = new();
    public List<RevenueBreakdownDto> ByPaymentLifecycle { get; set; } = new();
    public List<RevenueBreakdownDto> ByMonth { get; set; } = new();
}

public class RevenueBreakdownDto
{
    public string Key { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public int PaymentCount { get; set; }
    public decimal GrossAmount { get; set; }
    public decimal ProviderFeeAmount { get; set; }
    public decimal NetAmount { get; set; }
    public decimal AppliedToPackageAmount { get; set; }
    public decimal BalanceCreditAmount { get; set; }
}
