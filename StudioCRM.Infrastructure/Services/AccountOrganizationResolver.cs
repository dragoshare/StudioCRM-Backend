using Microsoft.EntityFrameworkCore;
using StudioCRM.Domain.Entities;
using StudioCRM.Infrastructure.Persistence;

namespace StudioCRM.Infrastructure.Services;

public sealed record AccountOrganizationResolution(Guid? OrganizationId, bool IsPlatformAdmin, bool CanAuthenticate);

public static class AccountOrganizationResolver
{
    // Only account binding metadata is read across tenants; no foreign CRM records leave this resolver.
    public static async Task<AccountOrganizationResolution> ResolveAsync(StudioCRMDbContext db, int userId)
    {
        var user = await db.Users.AsNoTracking().Where(u => u.Id == userId && u.IsActive)
            .Select(u => new { Roles = u.UserRoles.Select(r => r.Role.Name).ToList() }).SingleOrDefaultAsync();
        if (user == null) return new(null, false, false);
        if (user.Roles.Contains("SuperAdmin")) return new(null, true, true);

        var organizations = await db.Set<OrganizationMembership>().Where(m => m.UserId == userId)
            .Select(m => m.OrganizationId).ToListAsync();
        organizations.AddRange(await db.Clients.IgnoreQueryFilters().Where(c => c.UserId == userId)
            .Select(c => EF.Property<Guid>(c, "OrganizationId")).ToListAsync());
        organizations.AddRange(await db.Trainers.IgnoreQueryFilters().Where(t => t.UserId == userId)
            .Select(t => EF.Property<Guid>(t, "OrganizationId")).ToListAsync());
        if (user.Roles.Any(r => r is "Owner" or "Trainer" or "Client"))
            organizations.Add(StudioCRMDbContext.LegacyOrganizationId);

        var distinct = organizations.Distinct().ToArray();
        // Never pick the first studio when historical data violates the single-account rule.
        if (distinct.Length != 1) return new(null, false, false);
        var id = distinct[0];
        var status = await db.Organizations.Where(o => o.Id == id).Select(o => o.Status).SingleOrDefaultAsync();
        return new(id, false, status == "Active" ||
            (id == StudioCRMDbContext.LegacyOrganizationId && status == "LegacyActive"));
    }

    public static async Task<bool> MatchesContextAsync(StudioCRMDbContext db, int userId)
    {
        var binding = await ResolveAsync(db, userId);
        return binding.CanAuthenticate && (binding.IsPlatformAdmin || binding.OrganizationId == db.OrganizationId);
    }
}
