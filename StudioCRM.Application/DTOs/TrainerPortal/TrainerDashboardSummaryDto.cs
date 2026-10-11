namespace StudioCRM.Application.DTOs.TrainerPortal;
public class TrainerDashboardSummaryDto
{
    public int ActiveClientsCount { get; set; }
    public int TodaySessionsCount { get; set; }
    public int UpcomingSessionsCount { get; set; }
    public List<TrainerDashboardSessionDto> UpcomingSessions { get; set; } = new();
}
public class TrainerDashboardSessionDto
{
    public int Id { get; set; }
    public string Title { get; set; } = "";
    public DateTime StartAt { get; set; }
    public DateTime EndAt { get; set; }
    public string Status { get; set; } = "";
    public int LocationId { get; set; }
    public string LocationName { get; set; } = "";
    public int BookedSeats { get; set; }
    public int? Capacity { get; set; }
}
