using System.ComponentModel.DataAnnotations;
namespace StudioCRM.Application.DTOs.Branding;

public class BrandingSettingsDto
{
    [Required, StringLength(80, MinimumLength = 1)]
    public string ApplicationName { get; set; } = "ATLAS";
    [StringLength(240)]
    public string? WelcomeText { get; set; }
    [RegularExpression("^#[0-9a-fA-F]{6}$")]
    public string? PrimaryColor { get; set; }
    [RegularExpression("^#[0-9a-fA-F]{6}$")]
    public string? AccentColor { get; set; }
    public Guid? LightLogoAssetId { get; set; }
    public Guid? DarkLogoAssetId { get; set; }
    public Guid? IconAssetId { get; set; }
    public Guid? LoginImageAssetId { get; set; }
}
public class BrandingChangeRequest
{
    [Range(0, long.MaxValue)]
    public long ExpectedRevision { get; set; }
    [Required, StringLength(1000, MinimumLength = 1)]
    public string Reason { get; set; } = "";
}
public class SaveBrandingDraftRequest : BrandingChangeRequest
{
    [Required]
    public BrandingSettingsDto Settings { get; set; } = new();
}
public class RestoreBrandingRequest : BrandingChangeRequest
{
    [Range(1, int.MaxValue)]
    public int Version { get; set; }
}
public record PublicBrandingDto(Guid ProfileId, int Version, bool IsCustomized,
    string ApplicationName, string? WelcomeText, string? PrimaryColor, string? AccentColor,
    string? LightLogoUrl, string? DarkLogoUrl, string? IconUrl, string? LoginImageUrl);
public record BrandingEditorDto(Guid ProfileId, long Revision, int PublishedVersion,
    BrandingSettingsDto Draft, PublicBrandingDto Preview);
public record BrandingAssetDto(Guid Id, string Url);
public record BrandingVersionDto(int Version, int ActorUserId, DateTime CreatedAt, BrandingSettingsDto Settings);
public record BrandingAuditDto(long Id, int ActorUserId, string Action, string Reason,
    DateTime CreatedAt, string BeforeJson, string AfterJson);
