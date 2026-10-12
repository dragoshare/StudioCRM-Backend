using StudioCRM.Domain.Entities;
using StudioCRM.Infrastructure.Services;
namespace StudioCRM.Tests.IntegrationTests;

public class OrganizationRoleTests
{
    [PostgresFact]
    public async Task MembershipRolesDoNotLeakBetweenStudiosOrFallBackToGlobalRoles()
    {
        await using var test = await TestDatabase.CreateAsync();
        await using var setup = test.Context();
        var user = new User { Email = "multi@atlas.test" };
        setup.Add(new UserRole { User = user, Role = new Role { Name = "Owner" } });
        var a = Guid.NewGuid(); var b = Guid.NewGuid();
        foreach (var id in new[] { a, b })
        {
            var branding = new OrganizationBrandingProfile { Id = Guid.NewGuid(), Name = "Test" };
            setup.Add(branding);
            setup.Add(new Organization { Id = id, Name = "Test", Slug = id.ToString(), Status = "Active", BrandingProfileId = branding.Id });
        }
        await setup.SaveChangesAsync();
        setup.Add(new OrganizationMembership { OrganizationId = a, UserId = user.Id, IsTrainer = true });
        await setup.SaveChangesAsync();
        await using var first = test.Context(organizationId: a);
        await using var second = test.Context(organizationId: b);
        Assert.Equal(new[] { "Owner" }, await OrganizationAuthorization.RolesAsync(setup, user.Id));
        Assert.Equal(new[] { "Trainer" }, await OrganizationAuthorization.RolesAsync(first, user.Id));
        Assert.Empty(await OrganizationAuthorization.RolesAsync(second, user.Id));
        var member = await first.Set<OrganizationMembership>().FindAsync(a, user.Id);
        first.Remove(member!); await first.SaveChangesAsync();
        Assert.Empty(await OrganizationAuthorization.RolesAsync(first, user.Id));
        var org = await second.Organizations.FindAsync(b);
        org!.Status = "Draft";
        second.Add(new OrganizationMembership { OrganizationId = b, UserId = user.Id, IsOwner = true });
        await second.SaveChangesAsync();
        Assert.Empty(await OrganizationAuthorization.RolesAsync(second, user.Id));
    }

    [PostgresFact]
    public async Task SuperAdminDoesNotInheritOwnerRoleForCrm()
    {
        await using var test = await TestDatabase.CreateAsync();
        await using var db = test.Context();
        var user = new User { Email = "platform@atlas.test" };
        db.Add(new UserRole { User = user, Role = new Role { Name = "SuperAdmin" } });
        db.Add(new UserRole { User = user, Role = new Role { Name = "Owner" } });
        await db.SaveChangesAsync();
        Assert.Equal(new[] { "SuperAdmin" }, await OrganizationAuthorization.RolesAsync(db, user.Id));
        Assert.Equal(new[] { "SuperAdmin" }, (await PortalSecurityTests.Auth(db).GetMeAsync(user.Id))!.Roles);
    }
}