namespace StudioCRM.Application.DTOs.Billing;

public class TrainerSessionProfitabilityDto
{
    public int SessionId { get; set; }
    public string Title { get; set; } = string.Empty;
    public DateTime StartAt { get; set; }
    public DateTime EndAt { get; set; }

    public int TrainerId { get; set; }
    public string TrainerName { get; set; } = string.Empty;

    public int LocationId { get; set; }
    public string LocationName { get; set; } = string.Empty;
    public int? LegalEntityId { get; set; }
    public string? LegalEntityName { get; set; }

    public string SessionType { get; set; } = string.Empty;
    public int ParticipantsCount { get; set; }
    public decimal BillableHours { get; set; }
    public decimal HourlyRate { get; set; }

    public bool IsCoveredByContract { get; set; }
    public int? ContractId { get; set; }
    public string? ContractNumber { get; set; }

    public decimal RevenueAmount { get; set; }
    public decimal TrainerCostAmount { get; set; }
    public decimal PotentialTrainerCostAmount { get; set; }
    public decimal ProfitAmount { get; set; }
    public decimal? ProfitMarginPercent { get; set; }

    public List<TrainerSessionParticipantProfitabilityDto> Participants { get; set; } = new();
}
