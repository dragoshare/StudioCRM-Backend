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

    // NOWE — mail zasobu/sali z Outlooka, np. klaj_studio@...
    public string? LocationEmail { get; set; }

    public string? OrganizerEmail { get; set; }

    // NOWE — JSON z mailami uczestników Outlooka
    public string? AttendeesJson { get; set; }

    // NOWE — warningi mapowania, np. nierozpoznany klient / limit sali
    public string? MappingWarningsJson { get; set; }

    // NOWE — cykliczne eventy Outlooka
    public string? SeriesMasterId { get; set; }

    public bool IsRecurring { get; set; } = false;

    public bool IsConvertedToSession { get; set; } = false;

    public int? SessionId { get; set; }

    public DateTime ImportedAt { get; set; } = DateTime.UtcNow;

    public CalendarIntegration CalendarIntegration { get; set; } = null!;

    public Session? Session { get; set; }
   
}