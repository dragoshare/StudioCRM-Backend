using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using StudioCRM.Application.DTOs.Organizations;
using StudioCRM.Application.Interfaces;
using StudioCRM.Domain.Entities;
namespace StudioCRM.Infrastructure.Services.Branding;

public partial class PlatformOrganizationService
{
    private static void Validate(object request)
    {
        var errors = new List<ValidationResult>();
        if (!Validator.TryValidateObject(request, new ValidationContext(request), errors, true))
            throw new InvalidOperationException(string.Join(" ", errors.Select(x => x.ErrorMessage)));
    }
    private static void Page(int page, int pageSize)
    {
        if (page < 1 || page > 100000 || pageSize < 1 || pageSize > 100) throw new InvalidOperationException("Invalid pagination.");
    }
    private static void CheckRevision(Organization organization, long revision)
    {
        if (organization.Revision != revision) throw new DbUpdateConcurrencyException("Organization changed. Reload before saving.");
    }
    private static void ValidateNames(string name, string reason)
    {
        if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(reason)) throw new InvalidOperationException("Name and reason are required.");
    }
    private void Audit(Organization org, int actor, string action, string reason, object before, object after, int? subject = null) =>
        db.Set<PlatformAuditEntry>().Add(new() { OrganizationId = org.Id, ActorUserId = actor, SubjectUserId = subject,
            Action = action, Reason = reason.Trim(), BeforeJson = JsonSerializer.Serialize(before),
            AfterJson = JsonSerializer.Serialize(after), CreatedAt = DateTime.UtcNow });
    private async Task<User> EligibleOwner(int id) =>
        await db.Users.SingleOrDefaultAsync(x => x.Id == id && x.IsActive && x.EmailVerifiedAt != null
            && !x.UserRoles.Any(r => r.Role.Name == "SuperAdmin"))
        ?? throw new InvalidOperationException("Owner must be an active verified account without the platform SuperAdmin role.");

    public async Task<OrganizationSummaryDto> CreateAsync(CreateOrganizationRequest request)
    {
        var actor = await access.RequireAdminAsync(); Validate(request); ValidateNames(request.Name, request.Reason);
        if (await db.Organizations.AnyAsync(x => x.Slug == request.Slug)) throw new DbUpdateConcurrencyException("Slug already exists.");
        if (request.InitialOwnerUserId is int uid) await EligibleOwner(uid);
        var profile = new OrganizationBrandingProfile { Id = Guid.NewGuid(), Name = request.Name.Trim() };
        var org = new Organization { Id = Guid.NewGuid(), Name = request.Name.Trim(), Slug = request.Slug,
            UiVariant = request.UiVariant, BrandingProfileId = profile.Id, Status = "Draft" };
        db.Add(profile); db.Add(org);
        if (request.InitialOwnerUserId is int owner)
            db.Add(new OrganizationMembership { OrganizationId = org.Id, UserId = owner, IsOwner = true, CreatedAt = DateTime.UtcNow });
        Audit(org, actor, "OrganizationCreated", request.Reason, new { }, new { Organization = Summary(org), request.InitialOwnerUserId });
        await db.SaveChangesAsync();
        return Summary(org);
    }

    public async Task<OrganizationSummaryDto> UpdateAsync(Guid id, UpdateOrganizationRequest request)
    {
        var actor = await access.RequireAdminAsync(); Validate(request); ValidateNames(request.Name, request.Reason);
        var org = await db.Organizations.SingleOrDefaultAsync(x => x.Id == id) ?? throw new KeyNotFoundException("Organization not found.");
        CheckRevision(org, request.ExpectedRevision);
        if (await db.Organizations.AnyAsync(x => x.Id != id && x.Slug == request.Slug)) throw new DbUpdateConcurrencyException("Slug already exists.");
        var before = Summary(org);
        org.Name = request.Name.Trim(); org.Slug = request.Slug; org.UiVariant = request.UiVariant; org.Revision++;
        Audit(org, actor, "OrganizationUpdated", request.Reason, before, Summary(org));
        await db.SaveChangesAsync(); return Summary(org);
    }

    public async Task<OrganizationSummaryDto> AssignOwnerAsync(Guid id, int userId, AssignOrganizationOwnerRequest request)
    {
        var actor = await access.RequireAdminAsync(); Validate(request); ValidateNames("Owner", request.Reason);
        var org = await db.Organizations.SingleOrDefaultAsync(x => x.Id == id) ?? throw new KeyNotFoundException("Organization not found.");
        CheckRevision(org, request.ExpectedRevision);
        // Legacy global roles remain authoritative for the running single-studio CRM.
        // Do not pretend that editing a registry membership revokes/grants that live access.
        if (org.Status != "Draft") throw new DbUpdateConcurrencyException("Owner management for the legacy studio requires organization-scoped authorization. Only draft organizations are editable in this stage.");
        await EligibleOwner(userId);
        var membership = await db.Set<OrganizationMembership>().SingleOrDefaultAsync(x => x.OrganizationId == id && x.UserId == userId);
        if (membership?.IsOwner == true) return Summary(org);
        if (membership == null)
        {
            membership = new OrganizationMembership { OrganizationId = id, UserId = userId, CreatedAt = DateTime.UtcNow };
            db.Add(membership);
        }
        membership.IsOwner = true; org.Revision++;
        Audit(org, actor, "OwnerAssigned", request.Reason, new { IsOwner = false }, new { IsOwner = true }, userId);
        await db.SaveChangesAsync(); return Summary(org);
    }

    public async Task<OrganizationUserPageDto> UsersAsync(Guid id, string? role, int page, int pageSize)
    {
        var org = await RequireAsync(id); Page(page, pageSize);
        if (role != null && role != "Owner" && role != "Trainer") throw new InvalidOperationException("Role must be Owner or Trainer.");
        // During the transition show the actual legacy roles, including staff added after migration.
        if (org.Status == "LegacyActive" && org.Id == StudioCRM.Infrastructure.Persistence.StudioCRMDbContext.LegacyOrganizationId)
        {
            var legacy = db.Users.AsNoTracking().Where(u => u.UserRoles.Any(r => r.Role.Name == "Owner" || r.Role.Name == "Trainer"));
            if (role != null) legacy = legacy.Where(u => u.UserRoles.Any(r => r.Role.Name == role));
            var count = await legacy.CountAsync();
            var staff = await legacy.OrderBy(u => u.LastName).ThenBy(u => u.Id).Skip((page-1)*pageSize).Take(pageSize)
                .Select(u => new { u.Id, u.FirstName, u.LastName, u.Email, u.IsActive,
                    Owner = u.UserRoles.Any(r => r.Role.Name == "Owner"), Trainer = u.UserRoles.Any(r => r.Role.Name == "Trainer") }).ToListAsync();
            return new(staff.Select(u => new OrganizationUserDto(u.Id, (u.FirstName+" "+u.LastName).Trim(), u.Email,
                new[] { u.Owner ? "Owner" : null, u.Trainer ? "Trainer" : null }.OfType<string>().ToArray(),
                u.IsActive ? "Active" : "Inactive", "Legacy")).ToList(), count, page, pageSize);
        }
        var query = db.Set<OrganizationMembership>().AsNoTracking().Where(x => x.OrganizationId == id);
        if (role == "Owner") query = query.Where(x => x.IsOwner);
        if (role == "Trainer") query = query.Where(x => x.IsTrainer);
        var total = await query.CountAsync();
        var rows = await query.OrderBy(x => x.User.LastName).ThenBy(x => x.UserId).Skip((page-1)*pageSize).Take(pageSize)
            .Select(x => new { x.UserId, x.User.FirstName, x.User.LastName, x.User.Email, x.User.IsActive, x.IsOwner, x.IsTrainer }).ToListAsync();
        return new(rows.Select(x => new OrganizationUserDto(x.UserId, (x.FirstName+" "+x.LastName).Trim(), x.Email,
            new[] { x.IsOwner ? "Owner" : null, x.IsTrainer ? "Trainer" : null }.OfType<string>().ToArray(),
            x.IsActive ? "Active" : "Inactive", "Registered")).ToList(), total, page, pageSize);
    }

    public async Task<PlatformUserPageDto> SearchUsersAsync(string query, int page, int pageSize)
    {
        await access.RequireAdminAsync(); Page(page, pageSize);
        if (string.IsNullOrWhiteSpace(query) || query.Trim().Length < 3 || query.Length > 100) throw new InvalidOperationException("Search must contain 3–100 characters.");
        var term = query.Trim().ToLowerInvariant();
        var users = db.Users.AsNoTracking().Where(x => !x.UserRoles.Any(r => r.Role.Name == "SuperAdmin") &&
            (x.Email.ToLower().Contains(term) || (x.FirstName+" "+x.LastName).ToLower().Contains(term)));
        var total = await users.CountAsync();
        var items = await users.OrderBy(x => x.Email).ThenBy(x => x.Id).Skip((page-1)*pageSize).Take(pageSize)
            .Select(x => new PlatformUserDto(x.Id, (x.FirstName+" "+x.LastName).Trim(), x.Email, x.IsActive ? "Active" : "Inactive", x.EmailVerifiedAt != null)).ToListAsync();
        return new(items, total, page, pageSize);
    }
}

