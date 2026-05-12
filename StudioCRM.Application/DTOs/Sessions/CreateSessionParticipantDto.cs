namespace StudioCRM.Application.DTOs.Sessions;

public class CreateSessionParticipantDto
{
    public int ClientId { get; set; }

    public bool CountsAgainstPackage { get; set; } = true;

    public int SessionsCharged { get; set; } = 1;

    public string? Note { get; set; }
}
