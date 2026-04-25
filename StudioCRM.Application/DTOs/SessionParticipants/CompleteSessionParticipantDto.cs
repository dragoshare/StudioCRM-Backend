namespace StudioCRM.Application.DTOs.SessionParticipants;

public class CompleteSessionParticipantDto
{
    public int ClientId { get; set; }

    public string AttendanceStatus { get; set; } = "Present";

    public bool CountsAgainstPackage { get; set; } = true;

    public int SessionsCharged { get; set; } = 1;

    public string? Note { get; set; }
}