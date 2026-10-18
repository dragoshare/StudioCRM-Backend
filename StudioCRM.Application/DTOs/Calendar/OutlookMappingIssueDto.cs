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

    // Stable type, e.g. CompletedSessionDeletedInOutlook, PastEventNeedsReview, UnknownClient, UnknownLocation,
    // UnknownTrainer, LocationLimitExceeded, Other.
    public string IssueType { get; set; } = string.Empty;

    // true: an Outlook change that waits for an owner decision about settlement (session correction,
    // cancellation or conversion). false: an import/mapping warning (unknown client, location, ...).
    public bool RequiresDecision { get; set; }

    // Owner of the Outlook calendar the event comes from (trainer or owner).
    public string? CalendarOwnerFullName { get; set; }

    // Human-readable reason; show it in the UI.
    public string Message { get; set; } = string.Empty;

    public string? Email { get; set; }

    public DateTime ImportedAt { get; set; }
}