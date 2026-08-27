using StudioCRM.Domain.Enums;

namespace StudioCRM.Application.DTOs.Billing;

public class ClientPaymentFilterDto
{
    public int? ClientId { get; set; }
    public int? LocationId { get; set; }
    public int? LegalEntityId { get; set; }
    public ClientPaymentStatus? Status { get; set; }
    public ClientPaymentSource? Source { get; set; }
    public ReceiptStatus? ReceiptStatus { get; set; }
    public string? PaymentProvider { get; set; }
    public DateTime? From { get; set; }
    public DateTime? To { get; set; }
    public decimal? AmountMin { get; set; }
    public decimal? AmountMax { get; set; }
    public bool? HasOverpayment { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 25;
}
