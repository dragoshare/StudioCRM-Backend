using Microsoft.EntityFrameworkCore;
using StudioCRM.Domain.Entities;
using StudioCRM.Infrastructure.Persistence;

namespace StudioCRM.Infrastructure.Services;

// Single source for the roles presented by Auth/me, issued in JWT and revalidated on requests.
public static class OrganizationAuthorization
{
    public static async Task<List<string>> RolesAsync(StudioCRMDbContext db, int userId)
    {
        var user = await db.Users.AsNoTracking().Where(u => u.Id == userId && u.IsActive)
            .Select(u => new { Roles = u.UserRoles.Select(r => r.Role.Name).ToList() }).SingleOrDefaultAsync();
        if (user == null) return [];
        // Platform administration never implicitly becomes CRM access or impersonation.
        if (user.Roles.Contains("SuperAdmin")) return ["SuperAdmin"];
        var status = await db.Organizations.Where(o => o.Id == db.OrganizationId).Select(o => o.Status).SingleOrDefaultAsync();
        if (status == "LegacyActive" && db.OrganizationId == StudioCRMDbContext.LegacyOrganizationId)
            return user.Roles.Where(r => r is "Owner" or "Trainer" or "Client").Distinct().ToList();
        if (status != "Active") return [];
        var membership = await db.Set<OrganizationMembership>().AsNoTracking()
            .SingleOrDefaultAsync(m => m.OrganizationId == db.OrganizationId && m.UserId == userId);
        var roles = new List<string>();
        if (membership?.IsOwner == true) roles.Add("Owner");
        if (membership?.IsTrainer == true) roles.Add("Trainer");
        if (await db.Clients.AnyAsync(c => c.UserId == userId && !c.PortalAccessBlocked)) roles.Add("Client");
        return roles;
    }
}