namespace StudioCRM.Application.DTOs.Billing;

public class TrainerSessionParticipantProfitabilityDto
{
    public int ClientId { get; set; }
    public string ClientName { get; set; } = string.Empty;
    public int? ClientPackageId { get; set; }
    public string? PackageName { get; set; }
    public string AttendanceStatus { get; set; } = string.Empty;
    public int SessionsCharged { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal RevenueAmount { get; set; }
    public decimal AllocatedTrainerCostAmount { get; set; }
    public decimal PotentialAllocatedTrainerCostAmount { get; set; }
    public decimal ProfitAmount { get; set; }
    public decimal? ProfitMarginPercent { get; set; }
}
