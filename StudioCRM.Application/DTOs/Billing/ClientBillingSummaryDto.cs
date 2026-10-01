namespace StudioCRM.Application.DTOs.Billing;

public class ClientBillingSummaryDto
{
    public int ClientId { get; set; }
    public string ClientName { get; set; } = string.Empty;

    public decimal CurrentBalance { get; set; }
    public decimal ActivePackageTotalPrice { get; set; }
    public decimal ActivePackageAmountPaid { get; set; }
    public decimal ActivePackageAmountDue { get; set; }

    public int? ActiveClientPackageId { get; set; }
    public string? ActivePackageName { get; set; }
    public string ActivePackagePaymentStatus { get; set; } = string.Empty;

    public List<ClientPackageBillingDto> Packages { get; set; } = new();
    public List<ClientPaymentDto> Payments { get; set; } = new();
}
