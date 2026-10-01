namespace StudioCRM.Domain.Entities;
// Registry only: operational tenant isolation is a separate migration stage.
public class Organization
{
    public Guid Id { get; set; }
    public string Name { get; set; } = "";
    public string Slug { get; set; } = "";
    public string UiVariant { get; set; } = "standard";
    public Guid BrandingProfileId { get; set; }
}
