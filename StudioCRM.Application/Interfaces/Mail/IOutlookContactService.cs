namespace StudioCRM.Application.Interfaces.Calendar;

public interface IOutlookContactService
{
    Task SyncClientsAsync();
}