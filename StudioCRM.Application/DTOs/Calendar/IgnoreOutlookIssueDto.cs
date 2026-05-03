namespace StudioCRM.Application.DTOs.Calendar;

public class IgnoreOutlookIssueDto
{
    public int ExternalCalendarEventId { get; set; }
    public string Message { get; set; } = string.Empty;
}