namespace StudioCRM.Domain.Entities;

public class OrganizationBrandingProfile
{
    public Guid Id { get; set; }
    public string Name { get; set; } = "";
    public long Revision { get; set; }
    public int PublishedVersion { get; set; }
    public string DraftJson { get; set; } = "{}";
    public string? PublishedJson { get; set; }
}
public class OrganizationBrandingVersion
{
    public Guid ProfileId { get; set; }
    public int Version { get; set; }
    public string SettingsJson { get; set; } = "{}";
    public int ActorUserId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
public class OrganizationBrandingAsset
{
    public Guid Id { get; set; }
    public Guid ProfileId { get; set; }
    public string Url { get; set; } = "";
    public string StorageKey { get; set; } = "";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
public class OrganizationBrandingAudit
{
    public long Id { get; set; }
    public Guid ProfileId { get; set; }
    public int ActorUserId { get; set; }
    public string Action { get; set; } = "";
    public string Reason { get; set; } = "";
    public string BeforeJson { get; set; } = "{}";
    public string AfterJson { get; set; } = "{}";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
