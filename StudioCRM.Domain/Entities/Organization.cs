namespace StudioCRM.Domain.Entities;
// Registry only: operational tenant isolation is a separate migration stage.
public class Organization
{
    public Guid Id { get; set; }
    public string Name { get; set; } = "";
    public string Slug { get; set; } = "";
    public string UiVariant { get; set; } = "standard";
    public string Status { get; set; } = "Draft";
    public long Revision { get; set; }
    public Guid BrandingProfileId { get; set; }
}
