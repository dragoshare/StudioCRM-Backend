namespace StudioCRM.Application.Interfaces.Calendar;

public interface IOutlookWebhookService
{
    Task HandleNotificationAsync(string requestBody);
}