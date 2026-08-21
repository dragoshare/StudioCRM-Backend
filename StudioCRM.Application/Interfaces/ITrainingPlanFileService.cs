using StudioCRM.Application.DTOs.TrainingPlans;

namespace StudioCRM.Application.Interfaces;

public interface ITrainingPlanFileService
{
    Task<TrainingPlanDto> UploadAsync(
        int clientId,
        Stream content,
        string fileName,
        string? contentType,
        long contentLength,
        CancellationToken cancellationToken = default);

    Task<TrainingPlanFileDownloadDto> DownloadAsync(
        int clientId,
        CancellationToken cancellationToken = default);

    Task<TrainingPlanFileDownloadDto> DownloadCurrentClientAsync(
        CancellationToken cancellationToken = default);

    Task<TrainingPlanDto> DeleteAsync(
        int clientId,
        CancellationToken cancellationToken = default);
}
