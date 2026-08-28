namespace StudioCRM.Application.DTOs.Billing;

public class TrainerCostStatisticsDto
{
    public DateTime? From { get; set; }
    public DateTime? To { get; set; }

    public int SessionsCount { get; set; }
    public int CoveredSessionsCount { get; set; }
    public int UncoveredSessionsCount { get; set; }
    public int ParticipantsCount { get; set; }
    public decimal BillableHours { get; set; }
    public decimal RevenueAmount { get; set; }
    public decimal TrainerCostAmount { get; set; }
    public decimal PotentialTrainerCostAmount { get; set; }
    public decimal UncoveredPotentialTrainerCostAmount { get; set; }
    public decimal ProfitAmount { get; set; }
    public decimal? ProfitMarginPercent { get; set; }

    public List<TrainerCostBreakdownDto> ByTrainer { get; set; } = new();
    public List<TrainerCostBreakdownDto> ByLocation { get; set; } = new();
    public List<TrainerCostBreakdownDto> ByLegalEntity { get; set; } = new();
    public List<TrainerCostBreakdownDto> ByClient { get; set; } = new();
    public List<TrainerCostBreakdownDto> ByPackage { get; set; } = new();
    public List<TrainerCostBreakdownDto> ByMonth { get; set; } = new();
}
