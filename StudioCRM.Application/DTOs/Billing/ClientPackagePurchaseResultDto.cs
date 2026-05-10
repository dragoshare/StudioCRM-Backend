namespace StudioCRM.Application.DTOs.Billing;

public class ClientPackagePurchaseResultDto
{
    public int ClientPackageId { get; set; }
    public int PaymentId { get; set; }
    public string PackageName { get; set; } = string.Empty;
    public decimal PackagePrice { get; set; }
    public decimal PaymentAmount { get; set; }
    public string PaymentStatus { get; set; } = string.Empty;
    public string ActivationMode { get; set; } = string.Empty;
}
