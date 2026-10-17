namespace StudioCRM.Application.DTOs.Calendar;

public class OutlookReconciliationRequestDto
{
    public int PastDays { get; set; } = 30;

    public int FutureDays { get; set; } = 180;

    // true: only the signed-in user's own Outlook (an owner otherwise reconciles every trainer's calendar).
    public bool OnlyMine { get; set; }

    // Owner only: reconcile just this trainer's Outlook.
    public int? TrainerId { get; set; }
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

// The user a reconcile runs for; captured from the request so a background job keeps the same scope.
public record OutlookReconcileActor(int UserId, bool IsOwner, bool IsTrainer);
