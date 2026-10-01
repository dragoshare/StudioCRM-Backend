using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using StudioCRM.Api.Controllers;
using StudioCRM.Application.Interfaces.Storage;
using StudioCRM.Domain.Entities;
using StudioCRM.Infrastructure.Persistence;
using StudioCRM.Infrastructure.Services.Branding;
namespace StudioCRM.Tests.IntegrationTests;
public class PlatformOrganizationTests
{
    private static readonly Guid First = Guid.Parse("b5100000-0000-4000-8000-000000000002");
    private static readonly Guid Profile = Guid.Parse("b5100000-0000-4000-8000-000000000001");
    private static PlatformOrganizationService Service(StudioCRMDbContext db, int userId, string role = "SuperAdmin", Guid? profile = null) =>
        new(db, new BrandingAccess(db, new BrandingIntegrationTests.Current(userId, role)), new Storage(), new BrandingIntegrationTests.Scope(profile ?? Profile));
    [Fact]
    public void PlatformRequiresSuperAdminAndProfileCannotBeShared()
    {
        var auth = Assert.Single(typeof(PlatformOrganizationsController).GetCustomAttributes(typeof(AuthorizeAttribute), true).Cast<AuthorizeAttribute>());
        Assert.Equal("SuperAdmin", auth.Roles);
        using var db = new StudioCRMDbContext(new DbContextOptionsBuilder<StudioCRMDbContext>().UseNpgsql("Host=localhost;Database=model_only").Options);
        var entity = db.Model.FindEntityType(typeof(Organization))!;
        Assert.Contains(entity.GetIndexes(), i => i.IsUnique && i.Properties.Single().Name == "BrandingProfileId");
        Assert.Contains(entity.GetForeignKeys(), f => f.PrincipalEntityType.ClrType == typeof(OrganizationBrandingProfile) && f.DeleteBehavior == DeleteBehavior.Restrict);
    }
    [PostgresFact]
    public async Task RegistryScopesBrandingAndRejectsOwnerRevokedAndUnknownAccess()
    {
        await using var database = await TestDatabase.CreateAsync();
        await using var db = database.Context();
        var user = new User { Email = "platform@example.test", IsActive = true };
        db.Add(new UserRole { User = user, Role = new Role { Name = "SuperAdmin" } });
        var second = Guid.NewGuid(); var secondProfile = Guid.NewGuid();
        db.Add(new OrganizationBrandingProfile { Id = secondProfile, Name = "Second" });
        db.Add(new Organization { Id = second, Name = "Second", Slug = "second", BrandingProfileId = secondProfile });
        await db.SaveChangesAsync();
        var service = Service(db, user.Id);
        var listing = await service.ListAsync(1, 1);
        Assert.Equal(2, listing.Total); Assert.Single(listing.Items);
        Assert.Equal(First, (await service.GetCurrentAsync()).OrganizationId);
        Assert.Equal("bsworkout", (await service.GetAsync(First)).UiVariant);
        var branding = await service.BrandingAsync(second);
        await branding.SaveDraftAsync(new() { ExpectedRevision = 0, Reason = "Second studio", Settings = new() { ApplicationName = "Second custom" } });
        await branding.PublishAsync(new() { ExpectedRevision = 1, Reason = "Publish second" });
        Assert.Equal("Second custom", (await branding.GetPublishedAsync()).ApplicationName);
        Assert.False((await service.GetCurrentAsync()).Branding.IsCustomized);
        Assert.All(await db.OrganizationBrandingAudits.ToListAsync(), a => { Assert.Equal(secondProfile, a.ProfileId); Assert.Equal(user.Id, a.ActorUserId); });
        await Assert.ThrowsAsync<KeyNotFoundException>(() => service.BrandingAsync(Guid.NewGuid()));
        await Assert.ThrowsAsync<KeyNotFoundException>(() => Service(db, user.Id, profile: Guid.NewGuid()).GetCurrentAsync());
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.ListAsync(0, 25));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => Service(db, user.Id, "Owner").ListAsync(1, 25));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => Service(db, user.Id, "Owner").BrandingAsync(First));
        db.UserRoles.Remove(await db.UserRoles.SingleAsync()); await db.SaveChangesAsync();
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.GetAsync(First));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => branding.GetDraftAsync());
    }
    private sealed class Storage : IObjectStorageService
    {
        public Task<StoredObjectDto> UploadAsync(string key, byte[] content, string contentType, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<StoredObjectDownloadDto> DownloadAsync(string key, string? fileName = null, CancellationToken ct = default) => throw new NotSupportedException();
        public Task DeleteAsync(string key, CancellationToken ct = default) => throw new NotSupportedException();
    }
}
