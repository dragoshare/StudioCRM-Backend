namespace StudioCRM.Application.DTOs.SessionParticipants;

public class CompleteSessionDto
{
    public string ActualSessionType { get; set; } = "OneToOne";

    public List<CompleteSessionParticipantDto> Participants { get; set; } = new();
}