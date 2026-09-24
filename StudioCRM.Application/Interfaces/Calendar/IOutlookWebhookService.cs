namespace StudioCRM.Application.Interfaces.Calendar;

public interface IOutlookWebhookService
{
    Task HandleNotificationAsync(string requestBody);

    Task<StudioCRM.Application.DTOs.Calendar.OutlookReconciliationResultDto> ReconcileAsync(
        StudioCRM.Application.DTOs.Calendar.OutlookReconciliationRequestDto request);
}
