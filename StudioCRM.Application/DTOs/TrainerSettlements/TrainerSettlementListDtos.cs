using System.ComponentModel.DataAnnotations;

namespace StudioCRM.Application.DTOs.TrainerSettlements;

public class TrainerSettlementListFilter
{
    [Range(2000, 2100)]
    public int Year { get; set; }
    [Range(1, 12)]
    public int Month { get; set; }
    public int? LocationId { get; set; }
    [StringLength(100)]
    public string? Search { get; set; }
    [Range(1, 100000)]
    public int Page { get; set; } = 1;
    [Range(1, 100)]
    public int PageSize { get; set; } = 25;

    public void Validate() => Validator.ValidateObject(this, new ValidationContext(this), true);
}

// Same totals as GET /api/trainers/{id}/settlement (contracted sessions only).
public class TrainerSettlementListItemDto
{
    public int TrainerId { get; set; }
    public string TrainerFullName { get; set; } = string.Empty;
    public decimal TotalHours { get; set; }
    public int TotalSessions { get; set; }
    public decimal TotalAmount { get; set; }
    public decimal NonContractedTotalHours { get; set; }
    public int NonContractedTotalSessions { get; set; }
    // Calculated at the trainer rates, but not included in TotalAmount (no covering contract).
    public decimal NonContractedTotalAmount { get; set; }
    // Paid flag is stored per trainer and month, not per location.
    public bool IsPaid { get; set; }
    public DateTime? PaidAt { get; set; }
}

public class TrainerSettlementListSummaryDto
{
    public decimal TotalAmount { get; set; }
    public decimal PaidAmount { get; set; }
    public decimal UnpaidAmount { get; set; }
    public int TrainersCount { get; set; }
    public int PaidTrainersCount { get; set; }
}

public class TrainerSettlementListDto
{
    public int Year { get; set; }
    public int Month { get; set; }
    public int? LocationId { get; set; }
    public List<TrainerSettlementListItemDto> Items { get; set; } = new();
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalCount { get; set; }
    public int TotalPages { get; set; }
    // Covers every trainer matching the filter, not only the current page.
    public TrainerSettlementListSummaryDto Summary { get; set; } = new();
    public DateTime AsOf { get; set; }
    public string TimeZone { get; set; } = "Europe/Warsaw";
}
