namespace StudioCRM.Application.DTOs.TrainerSettlements;

public class TrainerSettlementItemDto
{
    public int SessionId { get; set; }

    public DateTime StartAt { get; set; }
    public DateTime EndAt { get; set; }

    public string Title { get; set; } = string.Empty;
    public string SessionType { get; set; } = string.Empty;

    public int LocationId { get; set; }
    public string LocationName { get; set; } = string.Empty;

    public bool IsCoveredByContract { get; set; }
    public int? ContractId { get; set; }
    public string? ContractNumber { get; set; }

    public decimal Hours { get; set; }
    public decimal Rate { get; set; }
    public decimal Amount { get; set; }

    public int ParticipantsCount { get; set; }
}
