namespace StudioCRM.Application.DTOs.SessionParticipants;

public class AddSessionParticipantDto
{
    public int ClientId { get; set; }

    public int? PackageId { get; set; }

    public bool CountsAgainstPackage { get; set; } = true;

    public int SessionsCharged { get; set; } = 1;

    public string? Note { get; set; }
}