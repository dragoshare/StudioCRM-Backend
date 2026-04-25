namespace StudioCRM.Domain.Entities;

public class ExternalCalendarEvent
{
    public int Id { get; set; }

    public int CalendarIntegrationId { get; set; }

    public string Provider { get; set; } = "Outlook";

    public string ExternalEventId { get; set; } = string.Empty;

    public string Subject { get; set; } = string.Empty;

    public string? BodyPreview { get; set; }

    public DateTime StartAt { get; set; }

    public DateTime EndAt { get; set; }

    public string? LocationName { get; set; }

    public string? OrganizerEmail { get; set; }

    public bool IsConvertedToSession { get; set; } = false;

    public int? SessionId { get; set; }

    public DateTime ImportedAt { get; set; } = DateTime.UtcNow;

    public CalendarIntegration CalendarIntegration { get; set; } = null!;

    public Session? Session { get; set; }
}