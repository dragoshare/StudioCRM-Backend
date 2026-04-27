namespace StudioCRM.Application.DTOs.TrainerSettlements;

public class TrainerMonthlySettlementDto
{
    public int TrainerId { get; set; }

    public string TrainerFullName { get; set; } = string.Empty;

    public int Year { get; set; }
    public int Month { get; set; }

    public decimal TotalHours { get; set; }
    public int TotalSessions { get; set; }
    public decimal TotalAmount { get; set; }

    public bool IsPaid { get; set; }
    public DateTime? PaidAt { get; set; }

    public List<TrainerSettlementItemDto> Items { get; set; } = new();
}