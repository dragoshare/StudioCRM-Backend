using StudioCRM.Infrastructure.Persistence;
namespace StudioCRM.Api.Security;
public sealed class RequestOrganizationContext
{
    private Guid id = StudioCRMDbContext.LegacyOrganizationId;
    private bool frozen;
    public Guid OrganizationId => id;
    public void SetValidated(Guid organizationId)
    {
        if (organizationId == Guid.Empty || (frozen && id != organizationId))
            throw new InvalidOperationException("Organization context cannot change after database resolution.");
        id = organizationId;
    }
    public Guid Freeze() { frozen = true; return id; }
}
