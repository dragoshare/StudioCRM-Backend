namespace StudioCRM.Domain.Entities;

public class Session
{
    public int Id { get; set; }

    public string Title { get; set; } = string.Empty;

    public string? Note { get; set; }

    public DateTime StartAt { get; set; }

    public DateTime EndAt { get; set; }

    public int TrainerId { get; set; }

    public int ClientId { get; set; }

    public int? PackageId { get; set; }

    public string? Location { get; set; }

    public string Status { get; set; } = "Planned";

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public int? CreatedBy { get; set; }

    public Trainer Trainer { get; set; } = null!;

    public Client Client { get; set; } = null!;

    public Package? Package { get; set; }

    public bool IsDeleted { get; set; } = false;
    public DateTime? DeletedAt { get; set; }
}