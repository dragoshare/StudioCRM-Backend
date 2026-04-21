namespace StudioCRM.Application.DTOs.Dashboard;

public class OwnerDashboardDto
{
    public int TrainersCount { get; set; }
    public int ActiveClientsCount { get; set; }
    public int PlannedSessionsCount { get; set; }
    public int ActivePackagesCount { get; set; }

    public List<DashboardSessionDto> TodaySessions { get; set; } = [];
    public List<DashboardSessionDto> TomorrowSessions { get; set; } = [];
    public List<DashboardClientDto> RecentClients { get; set; } = [];
}

public class DashboardSessionDto
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public DateTime StartAt { get; set; }
    public DateTime EndAt { get; set; }
    public string TrainerFullName { get; set; } = string.Empty;
    public string ClientFullName { get; set; } = string.Empty;
    public string? Location { get; set; }
    public string Status { get; set; } = string.Empty;
}

public class DashboardClientDto
{
    public int Id { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}