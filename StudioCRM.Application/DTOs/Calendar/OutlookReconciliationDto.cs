namespace StudioCRM.Application.DTOs.Calendar;

public class OutlookReconciliationRequestDto
{
    public int PastDays { get; set; } = 30;

    public int FutureDays { get; set; } = 180;
}

public class OutlookReconciliationResultDto
{
    public DateTime RangeStartAt { get; set; }

    public DateTime RangeEndAt { get; set; }

    public int IntegrationsProcessed { get; set; }

    public int OutlookEventsFound { get; set; }

    public int ImportedOrUpdatedEvents { get; set; }

    public int MissingOutlookEventsMarkedDeleted { get; set; }

    public int CrmSessionsSyncedToOutlook { get; set; }

    public List<string> SeriesRequiringAttention { get; set; } = new();

    public List<string> Errors { get; set; } = new();
}
