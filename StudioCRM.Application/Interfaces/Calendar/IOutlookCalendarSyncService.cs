namespace StudioCRM.Application.Interfaces.Calendar;

public interface IOutlookCalendarSyncService
{
    Task SyncSessionAsync(int sessionId);

    Task DeleteSessionEventAsync(int sessionId);
}