namespace StudioCRM.Domain.Entities;

public class CalendarEventLink
{
    public int Id { get; set; }

    public int SessionId { get; set; }

    public int CalendarIntegrationId { get; set; }

    public string Provider { get; set; } = "Outlook";

    public string ExternalEventId { get; set; } = string.Empty;

    public DateTime SyncedAt { get; set; } = DateTime.UtcNow;

    public Session Session { get; set; } = null!;

    public CalendarIntegration CalendarIntegration { get; set; } = null!;
}