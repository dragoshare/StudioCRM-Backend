using StudioCRM.Application.DTOs.Payments;

namespace StudioCRM.Application.DTOs.ClientPortal;

public class ClientPortalPaymentDto
{
    public int? ClientPackageId { get; set; }

    public string? PackageName { get; set; }

    public decimal AmountDue { get; set; }

    public string Currency { get; set; } = "PLN";

    public string BillingStatus { get; set; } = string.Empty;

    public DateTime? PaymentDueDate { get; set; }

    public PaymentInstructionsDto? Instructions { get; set; }
}
