using StudioCRM.Domain.Enums;

namespace StudioCRM.Application.DTOs.Billing;

public class RequestClientPackageRenewalDto
{
    public PaymentMethod Method { get; set; }
    public decimal? Amount { get; set; }
    public DateTime? PaymentDate { get; set; }
    public string? Note { get; set; }
}
