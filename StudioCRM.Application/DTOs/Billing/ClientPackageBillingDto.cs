namespace StudioCRM.Application.DTOs.Billing;

public class ClientPackageBillingDto
{
    public int ClientPackageId { get; set; }
    public int PackageId { get; set; }
    public string PackageName { get; set; } = string.Empty;

    public bool IsActive { get; set; }
    public string ActivationMode { get; set; } = string.Empty;

    public int TotalSessions { get; set; }
    public int SessionsPerWeek { get; set; }
    public int UsedSessions { get; set; }
    public int RemainingSessions { get; set; }

    public decimal TotalPrice { get; set; }
    public decimal OriginalPrice { get; set; }
    public decimal BalanceApplied { get; set; }
    public decimal ExpectedUnitPrice { get; set; }
    public decimal AmountPaid { get; set; }
    public decimal AmountDue { get; set; }
    public string Currency { get; set; } = "PLN";
    public string ExpectedBillingType { get; set; } = string.Empty;
    public int? LocationId { get; set; }
    public string? LocationName { get; set; }
    public string PaymentStatus { get; set; } = string.Empty;

    public DateTime PurchaseDate { get; set; }
    public DateTime? ValidUntil { get; set; }
    public DateTime? PaymentDueDate { get; set; }
    public DateTime? ActivatedAt { get; set; }
}
