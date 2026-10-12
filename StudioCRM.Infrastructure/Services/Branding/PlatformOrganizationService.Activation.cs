using Microsoft.EntityFrameworkCore;
using StudioCRM.Application.DTOs.Organizations;
using StudioCRM.Domain.Entities;
using StudioCRM.Infrastructure.Persistence;
namespace StudioCRM.Infrastructure.Services.Branding;
public partial class PlatformOrganizationService
{
    public async Task<OrganizationActivationReadinessDto> ActivationReadinessAsync(Guid organizationId)
    {
        var org = await RequireAsync(organizationId);
        var blockers = new List<OrganizationActivationBlockerDto>();
        var owners = db.Set<OrganizationMembership>().Where(m => m.OrganizationId == organizationId && m.IsOwner);
        var eligible = owners.Where(m => m.User.IsActive && m.User.EmailVerifiedAt != null
            && !m.User.UserRoles.Any(r => r.Role.Name == "SuperAdmin"));
        var count = await eligible.CountAsync();
        if (count == 0) blockers.Add(new("EligibleOwnerRequired", "Przypisz aktywne, zweryfikowane konto Ownera przeznaczone wyłącznie dla tego studia."));
        // Metadata-only cross-tenant check of assigned owners. No foreign personal data is returned.
        var ownerIds = owners.Select(m => m.UserId);
        var conflicts = await db.Set<OrganizationMembership>().AnyAsync(m => m.OrganizationId != organizationId && ownerIds.Contains(m.UserId));
        conflicts |= await db.Clients.IgnoreQueryFilters().AnyAsync(c => c.UserId.HasValue && ownerIds.Contains(c.UserId.Value)
            && EF.Property<Guid>(c, "OrganizationId") != organizationId);
        conflicts |= await db.Trainers.IgnoreQueryFilters().AnyAsync(t => ownerIds.Contains(t.UserId)
            && EF.Property<Guid>(t, "OrganizationId") != organizationId);
        if (organizationId != StudioCRMDbContext.LegacyOrganizationId)
            conflicts |= await db.UserRoles.AnyAsync(r => ownerIds.Contains(r.UserId)
                && (r.Role.Name == "Owner" || r.Role.Name == "Trainer" || r.Role.Name == "Client"));
        if (conflicts) blockers.Add(new("OwnerAccountBelongsToAnotherOrganization", "Co najmniej jedno konto Ownera jest powiązane z innym studiem. Wymagane osobne konto i email."));
        if (org.Status != "Draft") blockers.Add(new("OrganizationNotDraft", "Aktywacja dotyczy wyłącznie organizacji Draft."));
        // Keep this explicit until login, background jobs and public callbacks resolve the account's studio.
        blockers.Add(new("OrganizationRuntimeNotReady", "Logowanie oraz pełna obsługa CRM nowej organizacji nie są jeszcze uruchomione w tej wersji backendu."));
        return new(org.Id, org.Status, false, count, blockers);
    }
}