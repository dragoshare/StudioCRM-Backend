namespace StudioCRM.Application.DTOs.Auth;
public record UserOrganizationDto(Guid OrganizationId, string Name, string UiVariant, string Status,
    string[] Roles, bool IsCurrent, bool CanEnter, string? UnavailableReason);
public record UserOrganizationPageDto(List<UserOrganizationDto> Items, int Total, int Page, int PageSize);