namespace StudioCRM.Application.DTOs.TrainerContracts;

public class TrainerContractDto
{
    public int Id { get; set; }

    public int TrainerId { get; set; }

    public string ContractType { get; set; } = "B2B";

    public string ContractNumber { get; set; } = string.Empty;

    public DateTime SignedAt { get; set; }

    public DateTime ValidFrom { get; set; }

    public DateTime? ValidTo { get; set; }

    public List<int> LocationIds { get; set; } = new();

    public List<string> LocationNames { get; set; } = new();

    public string? Notes { get; set; }

    public bool IsActive { get; set; }

    public bool IsCurrent { get; set; }

    public bool IsExpired { get; set; }

    public int? DaysUntilEnd { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }
}
