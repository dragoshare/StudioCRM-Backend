using StudioCRM.Domain.Enums;

namespace StudioCRM.Application.DTOs.Billing;

public class ClientPaymentDto
{
    public int Id { get; set; }
    public int ClientId { get; set; }
    public string ClientName { get; set; } = string.Empty;

    public int? ClientPackageId { get; set; }
    public string? PackageName { get; set; }

    public int? LocationId { get; set; }
    public string? LocationName { get; set; }
    public int? LegalEntityId { get; set; }
    public string? LegalEntityName { get; set; }
    public int? PaymentProviderAccountId { get; set; }
    public string? PaymentProviderAccountName { get; set; }

    public decimal Amount { get; set; }
    public decimal AppliedToPackageAmount { get; set; }
    public decimal BalanceCreditAmount { get; set; }
    public string Currency { get; set; } = "PLN";

    public PaymentMethod Method { get; set; }
    public ClientPaymentStatus Status { get; set; }
    public ClientPaymentSource Source { get; set; }

    public DateTime PaymentDate { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? ConfirmedAt { get; set; }
    public DateTime? RejectedAt { get; set; }
    public DateTime? ReversedAt { get; set; }

    public int? CreatedByUserId { get; set; }
    public int? ConfirmedByUserId { get; set; }
    public int? RejectedByUserId { get; set; }
    public int? ReversedByUserId { get; set; }

    public string? Note { get; set; }
    public string? RejectionReason { get; set; }
    public string? ReversalReason { get; set; }
    public string? ExternalPaymentId { get; set; }
    public string? PaymentProvider { get; set; }
    public string? ProviderPaymentId { get; set; }
    public string? ProviderStatus { get; set; }
    public string? CheckoutUrl { get; set; }
    public DateTime? CheckoutExpiresAt { get; set; }
    public DateTime? WebhookReceivedAt { get; set; }

    public bool ReceiptRequired { get; set; }
    public string ReceiptStatus { get; set; } = string.Empty;
    public string? ReceiptNumber { get; set; }
    public DateTime? ReceiptIssuedAt { get; set; }
    public DateTime? ReceiptSentAt { get; set; }
    public int? ReceiptIssuedByUserId { get; set; }
    public string? ReceiptNote { get; set; }
}
