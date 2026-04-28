namespace StudioCRM.Application.DTOs.ClientPortal;

public class ClientActivePackageDto
{
    public int ClientPackageId { get; set; }
    public int PackageId { get; set; }

    public string PackageName { get; set; } = string.Empty;

    public int TotalSessions { get; set; }
    public int UsedSessions { get; set; }
    public int RemainingSessions { get; set; }

    public decimal PackagePrice { get; set; }
    public decimal ExpectedUnitPrice { get; set; }

    public string ExpectedBillingType { get; set; } = string.Empty;

    public string PaymentStatus { get; set; } = string.Empty;
    public bool IsPaid { get; set; }
    public bool IsOverdue { get; set; }

    public DateTime PurchaseDate { get; set; }
    public DateTime? ValidUntil { get; set; }
}