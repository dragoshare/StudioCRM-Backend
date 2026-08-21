using StudioCRM.Application.DTOs.Profiles;

namespace StudioCRM.Application.Interfaces;

public interface IAvatarService
{
    Task<AvatarDto> UploadCurrentUserAvatarAsync(
        Stream content,
        string fileName,
        string? contentType,
        long contentLength,
        CancellationToken cancellationToken = default);

    Task<AvatarDto> DeleteCurrentUserAvatarAsync(CancellationToken cancellationToken = default);

    Task<AvatarDto> UploadClientAvatarAsync(
        int clientId,
        Stream content,
        string fileName,
        string? contentType,
        long contentLength,
        CancellationToken cancellationToken = default);

    Task<AvatarDto> DeleteClientAvatarAsync(
        int clientId,
        CancellationToken cancellationToken = default);

    Task<AvatarDto> UploadTrainerAvatarAsync(
        int trainerId,
        Stream content,
        string fileName,
        string? contentType,
        long contentLength,
        CancellationToken cancellationToken = default);

    Task<AvatarDto> DeleteTrainerAvatarAsync(
        int trainerId,
        CancellationToken cancellationToken = default);
}
