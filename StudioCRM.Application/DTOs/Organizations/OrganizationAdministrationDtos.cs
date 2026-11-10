using System.ComponentModel.DataAnnotations;
namespace StudioCRM.Application.DTOs.Organizations;

public class CreateOrganizationRequest
{
    [Required, StringLength(100)] public string Name { get; set; } = "";
    [Required, RegularExpression("^[a-z0-9]+(?:-[a-z0-9]+)*$"), StringLength(100)] public string Slug { get; set; } = "";
    [Required, RegularExpression("^(default|bsworkout)$")] public string UiVariant { get; set; } = "default";
    [Range(1, int.MaxValue)] public int? InitialOwnerUserId { get; set; }
    [Required, StringLength(1000)] public string Reason { get; set; } = "";
}
public class UpdateOrganizationRequest
{
    [Required, StringLength(100)] public string Name { get; set; } = "";
    [Required, RegularExpression("^[a-z0-9]+(?:-[a-z0-9]+)*$"), StringLength(100)] public string Slug { get; set; } = "";
    [Required, RegularExpression("^(default|bsworkout)$")] public string UiVariant { get; set; } = "default";
    [Range(0, long.MaxValue)] public long ExpectedRevision { get; set; }
    [Required, StringLength(1000)] public string Reason { get; set; } = "";
}
public class AssignOrganizationOwnerRequest
{
    [Range(0, long.MaxValue)] public long ExpectedRevision { get; set; }
    [Required, StringLength(1000)] public string Reason { get; set; } = "";
}
public record OrganizationUserDto(int UserId, string FullName, string Email, string[] Roles,
    string AccountStatus, string MembershipStatus);
public record OrganizationUserPageDto(List<OrganizationUserDto> Items, int Total, int Page, int PageSize);
public record PlatformUserDto(int UserId, string FullName, string Email, string AccountStatus, bool EmailVerified);
public record PlatformUserPageDto(List<PlatformUserDto> Items, int Total, int Page, int PageSize);

public class ActivateOrganizationRequest
{
    [Range(0, long.MaxValue)] public long ExpectedRevision { get; set; }
    [Required, StringLength(1000)] public string Reason { get; set; } = "";
    public bool ConfirmCoreCrm { get; set; }
    // Compatibility with the initial activation UI.
    public bool ConfirmSetupOnly { get; set; }
}
