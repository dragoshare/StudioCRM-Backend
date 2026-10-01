using Microsoft.EntityFrameworkCore;
using StudioCRM.Application.Interfaces;
using StudioCRM.Application.Interfaces.Storage;
using StudioCRM.Infrastructure.Persistence;
using StudioCRM.Domain.Entities;
namespace StudioCRM.Infrastructure.Services.Branding;
public class PlatformOrganizationService(StudioCRMDbContext db, BrandingAccess access,
    IObjectStorageService storage, IBrandingProfileContext currentProfile) : IPlatformOrganizationService
{
    private sealed class ProfileScope(Guid id) : IBrandingProfileContext { public Guid ProfileId => id; }
    private static OrganizationSummaryDto Summary(Organization o) => new(o.Id, o.Name, o.Slug, o.UiVariant);
    private BrandingService For(Organization o) => new(db, new ProfileScope(o.BrandingProfileId), access, storage);
    public async Task<OrganizationPageDto> ListAsync(int page, int pageSize)
    {
        await access.RequireAdminAsync();
        if (page < 1 || page > 100000 || pageSize < 1 || pageSize > 100)
            throw new InvalidOperationException("Invalid pagination: page 1..100000, pageSize 1..100.");
        var query = db.Organizations.AsNoTracking();
        var total = await query.CountAsync();
        var items = await query.OrderBy(o => o.Name).ThenBy(o => o.Id)
            .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();
        return new(items.Select(Summary).ToList(), total, page, pageSize);
    }
    private async Task<Organization> RequireAsync(Guid id)
    {
        await access.RequireAdminAsync();
        return await db.Organizations.AsNoTracking().SingleOrDefaultAsync(o => o.Id == id)
            ?? throw new KeyNotFoundException("Organization not found.");
    }
    public async Task<OrganizationSummaryDto> GetAsync(Guid organizationId) => Summary(await RequireAsync(organizationId));
    public async Task<IBrandingService> BrandingAsync(Guid organizationId) => For(await RequireAsync(organizationId));
    public async Task<PublicOrganizationDto> GetCurrentAsync()
    {
        // Server configuration chooses the deployment's studio. No client-controlled tenant switch.
        var org = await db.Organizations.AsNoTracking().SingleOrDefaultAsync(o => o.BrandingProfileId == currentProfile.ProfileId)
            ?? throw new KeyNotFoundException("Current organization is not configured.");
        return new(org.Id, org.Name, org.UiVariant, await For(org).GetPublishedAsync());
    }
}
