using Microsoft.EntityFrameworkCore;
using StudioCRM.Application.DTOs.Organizations;
using StudioCRM.Application.Interfaces.Storage;
using StudioCRM.Domain.Entities;
using StudioCRM.Infrastructure.Persistence;
using StudioCRM.Infrastructure.Services.Branding;
namespace StudioCRM.Tests.IntegrationTests;

public class OrganizationAdministrationTests
{
    static readonly Guid Bs = Guid.Parse("b5100000-0000-4000-8000-000000000002");
    static PlatformOrganizationService Service(StudioCRMDbContext db, int user, string role = "SuperAdmin") => new(db,
        new BrandingAccess(db, new BrandingIntegrationTests.Current(user, role)), new Storage(),
        new BrandingIntegrationTests.Scope(Guid.Parse("b5100000-0000-4000-8000-000000000001")));
    static async Task<(int Admin, int Owner)> Seed(StudioCRMDbContext db)
    {
        var admin = new User { Email = "admin@atlas.test", EmailVerifiedAt = DateTime.UtcNow };
        var owner = new User { Email = "owner@atlas.test", EmailVerifiedAt = DateTime.UtcNow };
        db.Add(new UserRole { User = admin, Role = new Role { Name = "SuperAdmin" } }); db.Add(owner);
        await db.SaveChangesAsync(); return (admin.Id, owner.Id);
    }
    [PostgresFact]
    public async Task CreateAndAssignAreScopedAuditedAndDoNotGrantGlobalAccess()
    {
        await using var test = await TestDatabase.CreateAsync(); await using var db = test.Context();
        var ids = await Seed(db); var service = Service(db, ids.Admin);
        var demo = await service.CreateAsync(new() { Name = "Atlas Demo", Slug = "atlas-demo", Reason = "Test" });
        Assert.Equal("Draft", demo.Status); Assert.False(demo.CrmAccessEnabled);
        var assigned = await service.AssignOwnerAsync(demo.OrganizationId, ids.Owner, new() { ExpectedRevision = 0, Reason = "Owner assignment" });
        Assert.Equal(1, assigned.Revision);
        Assert.Single((await service.UsersAsync(demo.OrganizationId, "Owner", 1, 25)).Items);
        Assert.Empty((await service.UsersAsync(Bs, null, 1, 25)).Items);
        Assert.Empty(await db.UserRoles.Where(x => x.UserId == ids.Owner).ToListAsync());
        var audit = await db.Set<PlatformAuditEntry>().ToListAsync(); Assert.Equal(2, audit.Count);
        Assert.All(audit, x => Assert.Equal(ids.Admin, x.ActorUserId));
        await service.AssignOwnerAsync(demo.OrganizationId, ids.Owner, new() { ExpectedRevision = 1, Reason = "Retry" });
        Assert.Equal(2, await db.Set<PlatformAuditEntry>().CountAsync());
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => service.AssignOwnerAsync(Bs, ids.Owner, new() { Reason = "Not yet" }));
        Assert.Equal(Bs, (await service.GetCurrentAsync()).OrganizationId);
    }
    [PostgresFact]
    public async Task RejectsRevokedAdminUnverifiedOwnerAndStaleRevision()
    {
        await using var test = await TestDatabase.CreateAsync(); await using var db = test.Context();
        var ids = await Seed(db); var service = Service(db, ids.Admin);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => Service(db, ids.Owner, "Owner").CreateAsync(new()));
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateAsync(new() { Name = " ", Slug = "bad", Reason = "Test" }));
        var demo = await service.CreateAsync(new() { Name = "Demo", Slug = "demo", Reason = "Test" });
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.AssignOwnerAsync(demo.OrganizationId, ids.Admin, new() { Reason = "Forbidden" }));
        var owner = await db.Users.SingleAsync(x => x.Id == ids.Owner); owner.EmailVerifiedAt = null; await db.SaveChangesAsync();
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.AssignOwnerAsync(demo.OrganizationId, ids.Owner, new() { Reason = "Unverified" }));
        await service.UpdateAsync(demo.OrganizationId, new() { Name = "Renamed", Slug = "renamed", Reason = "Rename", ExpectedRevision = 0 });
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => service.UpdateAsync(demo.OrganizationId, new() { Name = "Stale", Slug = "stale", Reason = "Stale", ExpectedRevision = 0 }));
        db.UserRoles.Remove(await db.UserRoles.SingleAsync()); await db.SaveChangesAsync();
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.UsersAsync(demo.OrganizationId, null, 1, 25));
    }
    [PostgresFact]
    public async Task ConcurrentUpdatesCannotOverwriteAndSearchDoesNotExposeSecrets()
    {
        await using var test = await TestDatabase.CreateAsync(); await using var db = test.Context();
        var ids = await Seed(db); var service = Service(db, ids.Admin);
        var demo = await service.CreateAsync(new() { Name = "Demo", Slug = "demo", Reason = "Test", InitialOwnerUserId = ids.Owner });
        await using var other = test.Context();
        await other.Organizations.SingleAsync(x => x.Id == demo.OrganizationId);
        await service.UpdateAsync(demo.OrganizationId, new() { Name = "Winner", Slug = "winner", Reason = "Winner" });
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => Service(other, ids.Admin).UpdateAsync(demo.OrganizationId,
            new() { Name = "Loser", Slug = "loser", Reason = "Loser" }));
        await using var verify = test.Context();
        Assert.Equal("Winner", (await verify.Organizations.SingleAsync(x => x.Id == demo.OrganizationId)).Name);
        Assert.Equal(2, await verify.Set<PlatformAuditEntry>().CountAsync());
        Assert.Single((await service.SearchUsersAsync("owner", 1, 25)).Items);
        Assert.Empty((await service.SearchUsersAsync("admin", 1, 25)).Items);
        await Assert.ThrowsAsync<KeyNotFoundException>(() => service.UsersAsync(Guid.NewGuid(), null, 1, 25));
    }
    private sealed class Storage : IObjectStorageService
    {
        public Task<StoredObjectDto> UploadAsync(string key, byte[] content, string contentType, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<StoredObjectDownloadDto> DownloadAsync(string key, string? fileName = null, CancellationToken ct = default) => throw new NotSupportedException();
        public Task DeleteAsync(string key, CancellationToken ct = default) => throw new NotSupportedException();
    }
}
