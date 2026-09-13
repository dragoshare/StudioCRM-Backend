namespace StudioCRM.Application.Interfaces.Calendar;

public interface IOutlookCalendarSyncService
{
    Task SyncSessionAsync(int sessionId);

    Task SyncSessionSeriesAsync(
        string recurringGroupId,
        StudioCRM.Application.DTOs.Sessions.SessionRecurrenceDto recurrence);

    Task DeleteSessionEventAsync(int sessionId);
}
