namespace StudioCRM.Domain.Entities;

public class TrainerContractLocation
{
    public int TrainerContractId { get; set; }

    public int LocationId { get; set; }

    public TrainerContract TrainerContract { get; set; } = null!;

    public Location Location { get; set; } = null!;
}
