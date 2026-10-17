namespace StudioCRM.Application.Interfaces.Calendar;

public interface IOutlookWebhookService
{
    Task HandleNotificationAsync(string requestBody);

    Task<StudioCRM.Application.DTOs.Calendar.OutlookReconciliationResultDto> ReconcileAsync(
        StudioCRM.Application.DTOs.Calendar.OutlookReconciliationRequestDto request);

    // Same reconcile for a captured user, e.g. from a background job where no HTTP user is available.
    Task<StudioCRM.Application.DTOs.Calendar.OutlookReconciliationResultDto> ReconcileAsync(
        StudioCRM.Application.DTOs.Calendar.OutlookReconciliationRequestDto request,
        StudioCRM.Application.DTOs.Calendar.OutlookReconcileActor actor);
}
