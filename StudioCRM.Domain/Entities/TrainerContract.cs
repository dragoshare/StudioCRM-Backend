using StudioCRM.Domain.Enums;

namespace StudioCRM.Domain.Entities;

public class TrainerContract
{
    public int Id { get; set; }

    public int TrainerId { get; set; }

    public TrainerContractType ContractType { get; set; } = TrainerContractType.B2B;

    public string ContractNumber { get; set; } = string.Empty;

    public DateTime SignedAt { get; set; }

    public DateTime ValidFrom { get; set; }

    public DateTime? ValidTo { get; set; }

    public string? Notes { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public Trainer Trainer { get; set; } = null!;
}
