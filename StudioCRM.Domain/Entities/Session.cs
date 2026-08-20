namespace StudioCRM.Domain.Entities;

public class Session
{
    public int Id { get; set; }

    public string Title { get; set; } = string.Empty;

    public string? Note { get; set; }

    public DateTime StartAt { get; set; }

    public DateTime EndAt { get; set; }

    public int TrainerId { get; set; }

    public int LocationId { get; set; }

    public string? StudioRoom { get; set; }

    public string Status { get; set; } = "Planned";
    public bool IsDeleted { get; set; } = false;

    public string? PlannedSessionType { get; set; }

    public string? ActualSessionType { get; set; }

    public int? ActualParticipantsCount { get; set; }

    public DateTime? CompletedAt { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public int? CreatedBy { get; set; }

    public Trainer Trainer { get; set; } = null!;

    public Location Location { get; set; } = null!;

    public ICollection<SessionParticipant> Participants { get; set; } = new List<SessionParticipant>();
    public string? OutlookCategoriesJson { get; set; }

    public string? OutlookCategoryColorsJson { get; set; }

    public string? PrimaryOutlookCategory { get; set; }
}
