using StudioCRM.Application.DTOs.Branding;
namespace StudioCRM.Application.Interfaces;
public record OrganizationSummaryDto(Guid OrganizationId, string Name, string Slug, string UiVariant);
public record OrganizationPageDto(List<OrganizationSummaryDto> Items, int Total, int Page, int PageSize);
public record PublicOrganizationDto(Guid OrganizationId, string Name, string UiVariant, PublicBrandingDto Branding);
public interface IPlatformOrganizationService
{
    Task<OrganizationPageDto> ListAsync(int page, int pageSize);
    Task<OrganizationSummaryDto> GetAsync(Guid organizationId);
    Task<IBrandingService> BrandingAsync(Guid organizationId);
    Task<PublicOrganizationDto> GetCurrentAsync();
}
