namespace StudioCRM.Application.DTOs.Organizations;
public record OrganizationActivationBlockerDto(string Code, string Message);
public record OrganizationActivationReadinessDto(Guid OrganizationId, string Status, bool CanActivate,
    int EligibleOwnerCount, List<OrganizationActivationBlockerDto> Blockers);