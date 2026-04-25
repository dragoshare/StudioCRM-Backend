namespace StudioCRM.Application.DTOs.Sessions;

public class SessionParticipantListDto
{
    public int Id { get; set; }

    public int ClientId { get; set; }

    public string ClientFullName { get; set; } = string.Empty;

    public int? PackageId { get; set; }

    public string? PackageName { get; set; }

    public string AttendanceStatus { get; set; } = string.Empty;

    public bool CountsAgainstPackage { get; set; }

    public int SessionsCharged { get; set; }

    public string? Note { get; set; }
}