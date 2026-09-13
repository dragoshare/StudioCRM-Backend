namespace StudioCRM.Application.DTOs.Sessions;

public class CreateSessionSeriesDto
{
    public CreateSessionDto Session { get; set; } = new();

    public SessionRecurrenceDto Recurrence { get; set; } = new();
}

public class SessionRecurrenceDto
{
    public string Frequency { get; set; } = "Weekly";

    public int Interval { get; set; } = 1;

    public List<string> DaysOfWeek { get; set; } = new();

    public DateTime? EndDate { get; set; }

    public int? OccurrencesCount { get; set; }
}

public class SessionSeriesDto
{
    public string RecurringGroupId { get; set; } = string.Empty;

    public string Frequency { get; set; } = string.Empty;

    public int Interval { get; set; }

    public int OccurrencesCount { get; set; }

    public bool OutlookSeriesSynced { get; set; }

    public string? OutlookSyncWarning { get; set; }

    public List<SessionDto> Sessions { get; set; } = new();
}
