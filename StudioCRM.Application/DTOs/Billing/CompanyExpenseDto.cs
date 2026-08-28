using StudioCRM.Domain.Enums;

namespace StudioCRM.Application.DTOs.Billing;

public class CompanyExpenseDto
{
    public int Id { get; set; }

    public int LegalEntityId { get; set; }
    public string LegalEntityName { get; set; } = string.Empty;

    public int? LocationId { get; set; }
    public string? LocationName { get; set; }

    public ExpenseCategory Category { get; set; }
    public ExpensePaymentStatus PaymentStatus { get; set; }

    public string VendorName { get; set; } = string.Empty;
    public string? VendorNip { get; set; }
    public string? InvoiceNumber { get; set; }

    public DateTime IssueDate { get; set; }
    public DateTime? SaleDate { get; set; }
    public DateTime? DueDate { get; set; }
    public DateTime? PaidAt { get; set; }

    public decimal NetAmount { get; set; }
    public decimal VatAmount { get; set; }
    public decimal GrossAmount { get; set; }
    public string Currency { get; set; } = "PLN";

    public string? Description { get; set; }
    public string? Notes { get; set; }
    public string? AttachmentUrl { get; set; }
    public string? AttachmentFileName { get; set; }
    public string? AttachmentContentType { get; set; }

    public bool IsRecurring { get; set; }
    public string? RecurringGroupId { get; set; }

    public int? CreatedByUserId { get; set; }
    public int? PaidByUserId { get; set; }

    public bool IsOverdue { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
