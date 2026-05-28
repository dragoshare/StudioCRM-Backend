namespace StudioCRM.Application.DTOs.TrainerContracts;

public class CreateTrainerContractDto
{
    public string ContractType { get; set; } = "B2B";

    public string ContractNumber { get; set; } = string.Empty;

    public DateTime SignedAt { get; set; }

    public DateTime ValidFrom { get; set; }

    public DateTime? ValidTo { get; set; }

    public string? Notes { get; set; }
}
