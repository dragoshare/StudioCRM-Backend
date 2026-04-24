namespace StudioCRM.Application.DTOs.ClientPortal;

public class ClientPortalDashboardDto
{
    public string GreetingName { get; set; } = string.Empty;

    public string GreetingMessage { get; set; } = string.Empty;

    public ClientPortalMeDto Me { get; set; } = new();

    public ClientPortalSessionDto? NextSession { get; set; }

    public ClientPortalTrainerDto? Trainer { get; set; }

    public ClientPortalPackageDto Package { get; set; } = new();

    public ClientPortalPaymentDto Payment { get; set; } = new();

    public List<ClientPortalSessionDto> UpcomingSessions { get; set; } = new();

    public List<ClientPortalSessionDto> RecentSessions { get; set; } = new();
}