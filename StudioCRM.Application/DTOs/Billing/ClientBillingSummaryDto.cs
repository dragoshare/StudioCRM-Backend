namespace StudioCRM.Application.DTOs.Billing;

public class ClientBillingSummaryDto
{
    public int ClientId { get; set; }
    public string ClientName { get; set; } = string.Empty;

    public decimal CurrentBalance { get; set; }
    public decimal ActivePackageTotalPrice { get; set; }
    public decimal ActivePackageAmountPaid { get; set; }
    public decimal ActivePackageAmountDue { get; set; }

    // All outstanding package receivables, including inactive packages with unpaid debt.
    // Do not combine currencies or subtract the client's separate carry-over balance.
    public List<ClientOutstandingAmountDto> OutstandingAmounts { get; set; } = new();

    public int? ActiveClientPackageId { get; set; }
    public string? ActivePackageName { get; set; }
    public string ActivePackagePaymentStatus { get; set; } = string.Empty;

    public List<ClientPackageBillingDto> Packages { get; set; } = new();
    public List<ClientPaymentDto> Payments { get; set; } = new();
}

public class ClientOutstandingAmountDto
{
    public string Currency { get; set; } = string.Empty;
    public decimal AmountDue { get; set; }
    public int PackageCount { get; set; }
}
