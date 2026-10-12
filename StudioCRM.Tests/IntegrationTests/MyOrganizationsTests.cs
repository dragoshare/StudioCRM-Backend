using StudioCRM.Domain.Entities;
using StudioCRM.Infrastructure.Persistence;
namespace StudioCRM.Tests.IntegrationTests;
public class MyOrganizationsTests
{
    [PostgresFact]
    public async Task ListShowsOnlyOwnStudiosAndNeverOffersDraftAccess()
    {
        await using var test = await TestDatabase.CreateAsync();
        await using var db = test.Context();
        var user = new User { Email = "multi@atlas.test" };
        db.Add(new UserRole { User = user, Role = new Role { Name = "Owner" } });
        var own = Guid.NewGuid(); var hidden = Guid.NewGuid();
        foreach (var id in new[] { own, hidden })
        {
            var profile = new OrganizationBrandingProfile { Id = Guid.NewGuid(), Name = "Demo" };
            db.Add(profile); db.Add(new Organization { Id = id, Name = "Demo", Slug = id.ToString(), BrandingProfileId = profile.Id });
        }
        await db.SaveChangesAsync();
        db.Add(new OrganizationMembership { UserId = user.Id, OrganizationId = own, IsTrainer = true });
        await db.SaveChangesAsync();
        var service = PortalSecurityTests.Auth(db);
        var result = await service.GetOrganizationsAsync(user.Id, 1, 25);
        Assert.Equal(2, result.Total);
        Assert.DoesNotContain(result.Items, i => i.OrganizationId == hidden);
        var draft = Assert.Single(result.Items, i => i.OrganizationId == own);
        Assert.Equal(new[] { "Trainer" }, draft.Roles);
        Assert.False(draft.CanEnter); Assert.False(draft.IsCurrent);
        Assert.Equal("OrganizationNotActive", draft.UnavailableReason);
        var current = Assert.Single(result.Items, i => i.OrganizationId == StudioCRMDbContext.LegacyOrganizationId);
        Assert.True(current.CanEnter); Assert.True(current.IsCurrent);
        Assert.Equal(new[] { "Owner" }, current.Roles);
        var page = await service.GetOrganizationsAsync(user.Id, 2, 1);
        Assert.Equal(2, page.Total); Assert.Single(page.Items);
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.GetOrganizationsAsync(user.Id, 0, 25));
        Assert.Empty((await service.GetOrganizationsAsync(int.MaxValue, 1, 25)).Items);
        user.IsActive = false; await db.SaveChangesAsync();
        Assert.Empty((await service.GetOrganizationsAsync(user.Id, 1, 25)).Items);
    }
    [PostgresFact]
    public async Task PlatformAccountDoesNotGetImplicitStudioAccess()
    {
        await using var test = await TestDatabase.CreateAsync(); await using var db = test.Context();
        var user = new User { Email = "admin@atlas.test" };
        db.Add(new UserRole { User = user, Role = new Role { Name = "SuperAdmin" } });
        db.Add(new UserRole { User = user, Role = new Role { Name = "Owner" } });
        await db.SaveChangesAsync();
        Assert.Empty((await PortalSecurityTests.Auth(db).GetOrganizationsAsync(user.Id, 1, 25)).Items);
    }
}