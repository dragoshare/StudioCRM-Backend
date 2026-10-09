namespace StudioCRM.Application.DTOs.TrainerPortal;

public class TrainerSessionParticipantDto
{
    public int ClientId { get; set; }
    public string ClientFullName { get; set; } = string.Empty;
    public string AttendanceStatus { get; set; } = string.Empty;
    public string ProfileUrl { get; set; } = string.Empty;
}

// A session-scoped profile intentionally contains no financial or private client notes.
public class TrainerParticipantProfileDto
{
    public int ClientId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string AttendanceStatus { get; set; } = string.Empty;
    public int SessionId { get; set; }
    public int LocationId { get; set; }
}
