namespace StudioCRM.Application.DTOs.Calendar;

public class OutlookMappingIssueDto
{
    public int ExternalCalendarEventId { get; set; }

    public int? SessionId { get; set; }

    public string Subject { get; set; } = string.Empty;

    public DateTime StartAt { get; set; }

    public DateTime EndAt { get; set; }

    public string? LocationName { get; set; }

    public string? OrganizerEmail { get; set; }

    public string IssueType { get; set; } = string.Empty;

    public string Message { get; set; } = string.Empty;

    public string? Email { get; set; }

    public DateTime ImportedAt { get; set; }
}