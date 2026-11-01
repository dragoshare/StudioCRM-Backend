namespace StudioCRM.Application.Interfaces.Calendar;

public interface IOutlookContactService
{
    Task SyncClientsAsync();
    // Background push of one client to every connected Outlook in the current organization, with the same
    // visibility rules as SyncClientsAsync. Failures of one calendar are logged and do not stop the others.
    Task SyncClientToConnectedOutlooksAsync(int clientId);
    Task<int> PrepareCalendarAddressesAsync();
}
