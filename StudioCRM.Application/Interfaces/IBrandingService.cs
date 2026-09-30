using StudioCRM.Application.DTOs.Branding;
namespace StudioCRM.Application.Interfaces;

public interface IBrandingService
{
    Task<PublicBrandingDto> GetPublishedAsync();
    Task<BrandingEditorDto> GetDraftAsync();
    Task<BrandingEditorDto> SaveDraftAsync(SaveBrandingDraftRequest request);
    Task<PublicBrandingDto> PublishAsync(BrandingChangeRequest request);
    Task<BrandingEditorDto> RestoreDraftAsync(RestoreBrandingRequest request);
    Task<List<BrandingVersionDto>> GetVersionsAsync(int page, int pageSize);
    Task<List<BrandingAuditDto>> GetAuditAsync(int page, int pageSize);
    Task<BrandingAssetDto> UploadAsync(Stream stream, CancellationToken ct);
}
public interface IBrandingProfileContext { Guid ProfileId { get; } }
