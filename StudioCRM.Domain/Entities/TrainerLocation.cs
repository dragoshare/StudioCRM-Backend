namespace StudioCRM.Domain.Entities;

public class TrainerLocation
{
    public int TrainerId { get; set; }

    public int LocationId { get; set; }

    public Trainer Trainer { get; set; } = null!;

    public Location Location { get; set; } = null!;
}