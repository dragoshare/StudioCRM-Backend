using Microsoft.EntityFrameworkCore;
using StudioCRM.Domain.Entities;
using StudioCRM.Infrastructure.Persistence;
namespace StudioCRM.Infrastructure.Services;

internal static class LegacyStaffMembership
{
    // These account creation flows still write global roles. Never use them for another studio.
    internal static async Task RequireLegacyAsync(StudioCRMDbContext db)
    {
        if (db.OrganizationId != StudioCRMDbContext.LegacyOrganizationId ||
            !await db.Organizations.AnyAsync(o => o.Id == db.OrganizationId && o.Status == "LegacyActive"))
            throw new InvalidOperationException("This account creation flow is only available for the legacy studio.");
    }

    internal static async Task TrackAsync(StudioCRMDbContext db, int userId, string grantedRole)
    {
        if (grantedRole is not ("Owner" or "Trainer")) return;
        await RequireLegacyAsync(db);
        var roles = await db.UserRoles.Where(r => r.UserId == userId).Select(r => r.Role.Name).ToListAsync();
        if (roles.Contains("SuperAdmin")) throw new InvalidOperationException("Platform administrator cannot become studio staff.");
        var membership = await db.Set<OrganizationMembership>().FindAsync(db.OrganizationId, userId);
        if (membership == null)
        {
            membership = new OrganizationMembership { OrganizationId = db.OrganizationId, UserId = userId, CreatedAt = DateTime.UtcNow };
            db.Add(membership);
        }
        membership.IsOwner |= grantedRole == "Owner" || roles.Contains("Owner");
        membership.IsTrainer |= grantedRole == "Trainer" || roles.Contains("Trainer");
    }
}