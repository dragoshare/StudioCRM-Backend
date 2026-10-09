namespace StudioCRM.Application.DTOs.Sessions;

public class CreateSessionDto
{
    public string Title { get; set; } = string.Empty;

    public string? Note { get; set; }

    public string? EventRules { get; set; }
    [System.ComponentModel.DataAnnotations.Range(0, 525600)]
    public int RegistrationClosesBeforeMinutes { get; set; } = 30;
    [System.ComponentModel.DataAnnotations.Range(0, 525600)]
    public int CancellationClosesBeforeMinutes { get; set; } = 720;

    public DateTime StartAt { get; set; }

    public DateTime? EndAt { get; set; }

    public int TrainerId { get; set; }

    public int LocationId { get; set; }

    public string Status { get; set; } = "Planned";

    public bool IsPubliclyBookable { get; set; }

    public string? PublicSlug { get; set; }

    public int? PublicCapacity { get; set; }

    public string? PlannedSessionType { get; set; }

    public List<string> OutlookCategories { get; set; } = new();

    public List<CreateSessionParticipantDto> Participants { get; set; } = new();
}
