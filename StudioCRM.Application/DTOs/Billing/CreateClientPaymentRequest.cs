using StudioCRM.Domain.Enums;

namespace StudioCRM.Application.DTOs.Billing;

public class CreateClientPaymentRequest
{
    public int? ClientId { get; set; }
    public int? ClientPackageId { get; set; }

    public decimal Amount { get; set; }
    public PaymentMethod Method { get; set; }

    public DateTime? PaymentDate { get; set; }
    public string? Note { get; set; }
}
