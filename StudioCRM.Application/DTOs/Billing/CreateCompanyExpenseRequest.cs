using StudioCRM.Domain.Enums;

namespace StudioCRM.Application.DTOs.Billing;

public class CreateCompanyExpenseRequest
{
    public int LegalEntityId { get; set; }
    public int? LocationId { get; set; }

    public ExpenseCategory Category { get; set; } = ExpenseCategory.Other;
    public ExpensePaymentStatus PaymentStatus { get; set; } = ExpensePaymentStatus.Unpaid;

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

    public bool IsRecurring { get; set; }
    public string? RecurringGroupId { get; set; }
    public DateTime? RecurrenceEndDate { get; set; }
    public int? RecurringOccurrencesCount { get; set; }
}
