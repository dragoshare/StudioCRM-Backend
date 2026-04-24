namespace StudioCRM.Application.DTOs.TrainerPortal;

public class TrainerPortalDashboardDto
{
    public TrainerPortalMeDto Me { get; set; } = new();

    public int ActiveClientsCount { get; set; }

    public int TodaySessionsCount { get; set; }

    public int UpcomingSessionsCount { get; set; }

    public List<TrainerPortalSessionDto> TodaySessions { get; set; } = new();

    public List<TrainerPortalSessionDto> UpcomingSessions { get; set; } = new();

    public List<TrainerPortalClientDto> RecentClients { get; set; } = new();
}