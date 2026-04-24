namespace StudioCRM.Application.DTOs.ClientPortal;

public class ClientPortalPaymentDto
{
    public decimal AmountDue { get; set; }

    public string Currency { get; set; } = "PLN";

    public string BillingStatus { get; set; } = string.Empty;

    public DateTime? PaymentDueDate { get; set; }
}