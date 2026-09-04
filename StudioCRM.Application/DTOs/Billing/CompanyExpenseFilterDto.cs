using StudioCRM.Domain.Enums;

namespace StudioCRM.Application.DTOs.Billing;

public class CompanyExpenseFilterDto
{
    public int? LegalEntityId { get; set; }
    public int? LocationId { get; set; }
    public ExpenseCategory? Category { get; set; }
    public ExpensePaymentStatus? PaymentStatus { get; set; }
    public DateTime? From { get; set; }
    public DateTime? To { get; set; }
    public DateTime? DueFrom { get; set; }
    public DateTime? DueTo { get; set; }
    public DateTime? PaidFrom { get; set; }
    public DateTime? PaidTo { get; set; }
    public string? Search { get; set; }
    public bool? IsRecurring { get; set; }
    public string? RecurringGroupId { get; set; }
    public bool? IsOverdue { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 25;
}
