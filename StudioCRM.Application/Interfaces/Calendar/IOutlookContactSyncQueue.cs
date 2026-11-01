namespace StudioCRM.Application.Interfaces.Calendar;

// Schedules a background push of a saved client to the connected Outlook contacts. Call after the change is
// committed; the request never waits for Microsoft Graph and Outlook errors never fail the client operation.
public interface IOutlookContactSyncQueue
{
    void Enqueue(Guid organizationId, int clientId);
}
