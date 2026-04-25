namespace StudioCRM.Application.DTOs.Calendar;

public class CalendarIntegrationStatusDto
{
    public bool IsConnected { get; set; }

    public string? Provider { get; set; }

    public string? Email { get; set; }

    public DateTime? ConnectedAt { get; set; }
}