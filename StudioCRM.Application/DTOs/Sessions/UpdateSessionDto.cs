namespace StudioCRM.Application.DTOs.Sessions;

public class UpdateSessionDto
{
    public string Title { get; set; } = string.Empty;

    public string? Note { get; set; }

    public string? EventRules { get; set; }
    [System.ComponentModel.DataAnnotations.Range(0, 525600)]
    public int? RegistrationClosesBeforeMinutes { get; set; }
    [System.ComponentModel.DataAnnotations.Range(0, 525600)]
    public int? CancellationClosesBeforeMinutes { get; set; }

    public DateTime StartAt { get; set; }

    public DateTime EndAt { get; set; }

    public int TrainerId { get; set; }

    public int LocationId { get; set; }

    public string Status { get; set; } = "Planned";

    public bool IsPubliclyBookable { get; set; }

    public string? PublicSlug { get; set; }

    public int? PublicCapacity { get; set; }

    public string? PlannedSessionType { get; set; }

    public List<string> OutlookCategories { get; set; } = new();

    public List<CreateSessionParticipantDto>? Participants { get; set; }

    public string? CorrectionReason { get; set; }
}
