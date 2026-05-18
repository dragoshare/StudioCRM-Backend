using StudioCRM.Domain.Enums;

namespace StudioCRM.Domain.Entities;

public class ClientPayment
{
    public int Id { get; set; }

    public int ClientId { get; set; }
    public Client Client { get; set; } = null!;

    public int? ClientPackageId { get; set; }
    public ClientPackage? ClientPackage { get; set; }

    public decimal Amount { get; set; }
    public decimal AppliedToPackageAmount { get; set; }
    public decimal BalanceCreditAmount { get; set; }
    public string Currency { get; set; } = "PLN";

    public PaymentMethod Method { get; set; }
    public ClientPaymentStatus Status { get; set; }
    public ClientPaymentSource Source { get; set; }

    public DateTime PaymentDate { get; set; } = DateTime.UtcNow;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
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

    public ReceiptStatus ReceiptStatus { get; set; } = ReceiptStatus.None;
    public string? ReceiptNumber { get; set; }
    public DateTime? ReceiptIssuedAt { get; set; }
}
