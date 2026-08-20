namespace StudioCRM.Application.DTOs.Calendar;

public class ExternalCalendarEventDto
{
    public int Id { get; set; }

    public string Subject { get; set; } = string.Empty;

    public string? BodyPreview { get; set; }

    public DateTime StartAt { get; set; }

    public DateTime EndAt { get; set; }

    public string? LocationName { get; set; }

    public string? LocationEmail { get; set; }

    public string? OrganizerEmail { get; set; }

    public bool IsConvertedToSession { get; set; }

    public int? SessionId { get; set; }

    public bool IsRecurring { get; set; }

    public string? SeriesMasterId { get; set; }

    public DateTime ImportedAt { get; set; }

    public List<string> Warnings { get; set; } = new();
    public List<string> Categories { get; set; } = new();
    public List<OutlookCategoryDto> CategoryColors { get; set; } = new();
}
