namespace StudioCRM.Application.DTOs.TrainerSettlements;

public class TrainerSettlementSummaryDto
{
    public int TrainerId { get; set; }
    public int? LocationId { get; set; }

    public string TrainerFullName { get; set; } = string.Empty;

    public int Year { get; set; }
    public int Month { get; set; }

    public decimal TotalHours { get; set; }
    public int TotalSessions { get; set; }
    public decimal TotalAmount { get; set; }

    public decimal ContractedTotalHours { get; set; }
    public int ContractedTotalSessions { get; set; }
    public decimal ContractedTotalAmount { get; set; }

    public decimal NonContractedTotalHours { get; set; }
    public int NonContractedTotalSessions { get; set; }

    public bool IsPaid { get; set; }
    public DateTime? PaidAt { get; set; }

}
