namespace StudioCRM.Application.DTOs.Sessions;

public class UpdateSessionDto
{
    public string Title { get; set; } = string.Empty;

    public string? Note { get; set; }

    public DateTime StartAt { get; set; }

    public DateTime EndAt { get; set; }

    public int TrainerId { get; set; }

    public int LocationId { get; set; }

    public string Status { get; set; } = "Planned";

    public string? PlannedSessionType { get; set; }

    public List<string> OutlookCategories { get; set; } = new();

    public List<CreateSessionParticipantDto> Participants { get; set; } = new();
}
