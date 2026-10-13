using StudioCRM.Application.DTOs.Organizations;
using StudioCRM.Application.DTOs.Branding;
namespace StudioCRM.Application.Interfaces;
public record OrganizationSummaryDto(Guid OrganizationId, string Name, string Slug, string UiVariant, string Status = "Draft", long Revision = 0, bool CrmAccessEnabled = false, string AccessMode = "Unavailable");
public record OrganizationPageDto(List<OrganizationSummaryDto> Items, int Total, int Page, int PageSize);
public record PublicOrganizationDto(Guid OrganizationId, string Name, string UiVariant, PublicBrandingDto Branding, string AccessMode = "Unavailable");
public interface IPlatformOrganizationService
{
    Task<OrganizationSummaryDto> ActivateAsync(Guid id, ActivateOrganizationRequest request);
    Task<OrganizationSummaryDto> CreateAsync(CreateOrganizationRequest request);
    Task<OrganizationSummaryDto> UpdateAsync(Guid id, UpdateOrganizationRequest request);
    Task<OrganizationSummaryDto> AssignOwnerAsync(Guid id, int userId, AssignOrganizationOwnerRequest request);
    Task<OrganizationUserPageDto> UsersAsync(Guid id, string? role, int page, int pageSize);
    Task<PlatformUserPageDto> SearchUsersAsync(string query, int page, int pageSize);
    Task<OrganizationPageDto> ListAsync(int page, int pageSize);
    Task<OrganizationSummaryDto> GetAsync(Guid organizationId);
    Task<OrganizationActivationReadinessDto> ActivationReadinessAsync(Guid organizationId);
    Task<IBrandingService> BrandingAsync(Guid organizationId);
    Task<PublicOrganizationDto> GetCurrentAsync();
}
