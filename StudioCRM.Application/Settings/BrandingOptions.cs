namespace StudioCRM.Application.Settings;

public class BrandingOptions
{
    // Stable initial profile; future organization context can resolve a different profile.
    public Guid ProfileId { get; set; } = Guid.Parse("b5100000-0000-4000-8000-000000000001");
}
