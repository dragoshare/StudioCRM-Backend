using Microsoft.EntityFrameworkCore;
using StudioCRM.Application.DTOs.Auth;
using StudioCRM.Domain.Entities;
using StudioCRM.Infrastructure.Persistence;
namespace StudioCRM.Infrastructure.Services;
public partial class AuthService
{
    public async Task<UserOrganizationPageDto> GetOrganizationsAsync(int userId, int page, int pageSize)
    {
        if (page < 1 || page > 100000 || pageSize < 1 || pageSize > 100)
            throw new InvalidOperationException("Invalid pagination.");
        var user = await _context.Users.AsNoTracking().Where(u => u.Id == userId && u.IsActive)
            .Select(u => new { Roles = u.UserRoles.Select(r => r.Role.Name).ToList() }).SingleOrDefaultAsync();
        if (user == null || user.Roles.Contains("SuperAdmin")) return new([], 0, page, pageSize);
        var legacyRoles = user.Roles.Where(r => r is "Owner" or "Trainer" or "Client").Distinct().ToArray();
        var memberships = _context.Set<OrganizationMembership>().AsNoTracking().Where(m => m.UserId == userId);
        var legacy = StudioCRMDbContext.LegacyOrganizationId;
        var query = _context.Organizations.AsNoTracking().Where(o => memberships.Any(m => m.OrganizationId == o.Id)
            || (o.Id == legacy && o.Status == "LegacyActive" && legacyRoles.Length > 0));
        var total = await query.CountAsync();
        var rows = await query.OrderBy(o => o.Name).ThenBy(o => o.Id).Skip((page - 1) * pageSize).Take(pageSize)
            .Select(o => new { o.Id, o.Name, o.UiVariant, o.Status,
                Owner = memberships.Any(m => m.OrganizationId == o.Id && m.IsOwner),
                Trainer = memberships.Any(m => m.OrganizationId == o.Id && m.IsTrainer) }).ToListAsync();
        return new(rows.Select(o =>
        {
            var isLegacy = o.Id == legacy && o.Status == "LegacyActive";
            var roles = isLegacy ? legacyRoles : new[] { o.Owner ? "Owner" : null, o.Trainer ? "Trainer" : null }.OfType<string>().ToArray();
            // One account has one studio; this endpoint describes access, not a studio switcher.
            var canEnter = (isLegacy || o.Status == "Active") && roles.Length > 0 && o.Id == _context.OrganizationId;
            return new UserOrganizationDto(o.Id, o.Name, o.UiVariant, o.Status, roles, canEnter, canEnter,
                canEnter ? null : o.Status == "Draft" ? "OrganizationNotActive" : "OrganizationSwitchingNotAvailable");
        }).ToList(), total, page, pageSize);
    }
}