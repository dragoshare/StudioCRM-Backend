namespace StudioCRM.Domain.Entities;

public class TrainerRate
{
    public int Id { get; set; }

    public int TrainerId { get; set; }

    public string SessionType { get; set; } = string.Empty;
    // OneToOne, TwoToOne, ThreeToOne, FourToOne

    public decimal Rate { get; set; }

    public DateTime ValidFrom { get; set; } = DateTime.UtcNow;
    public DateTime? ValidTo { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public Trainer Trainer { get; set; } = null!;
}