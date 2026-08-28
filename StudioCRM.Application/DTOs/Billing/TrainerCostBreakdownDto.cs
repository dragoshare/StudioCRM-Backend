namespace StudioCRM.Application.DTOs.Billing;

public class TrainerCostBreakdownDto
{
    public string Key { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public int SessionsCount { get; set; }
    public int ParticipantsCount { get; set; }
    public decimal BillableHours { get; set; }
    public decimal RevenueAmount { get; set; }
    public decimal TrainerCostAmount { get; set; }
    public decimal PotentialTrainerCostAmount { get; set; }
    public decimal ProfitAmount { get; set; }
    public decimal? ProfitMarginPercent { get; set; }
}
