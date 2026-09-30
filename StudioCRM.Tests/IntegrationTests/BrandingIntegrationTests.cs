using Microsoft.EntityFrameworkCore;
using StudioCRM.Application.Interfaces;
using StudioCRM.Application.Interfaces.Storage;
using StudioCRM.Domain.Entities;
using StudioCRM.Infrastructure.Persistence;
using StudioCRM.Infrastructure.Services.Branding;

namespace StudioCRM.Tests.IntegrationTests;

public class BrandingIntegrationTests
{
    private static readonly Guid Profile = Guid.Parse("b5100000-0000-4000-8000-000000000001");
    internal sealed class Scope(Guid id) : IBrandingProfileContext { public Guid ProfileId => id; }
    internal sealed class Current(int id, string role = "SuperAdmin") : ICurrentUserService
    {
        public int? UserId => id;
        public string? Email => null;
        public List<string> Roles => new() { role };
        public bool IsAuthenticated => true;
        public bool IsOwner => role == "Owner";
        public bool IsTrainer => false;
        public bool IsClient => false;
    }
    private static BrandingService Service(StudioCRMDbContext db, Current current, Guid? profile = null) =>
        new(db, new Scope(profile ?? Profile), new BrandingAccess(db, current), new Storage());

    [PostgresFact]
    public async Task DraftPublicationRestoreAndRevocation()
    {
        await using var database = await TestDatabase.CreateAsync();
        await using var db = database.Context();
        var user = new User { Email = "admin@example.test", IsActive = true };
        db.Add(new UserRole { User = user, Role = new Role { Name = "SuperAdmin" } });
        await db.SaveChangesAsync();
        var current = new Current(user.Id);
        var service = Service(db, current);
        Assert.False((await service.GetPublishedAsync()).IsCustomized);
        var draft = await service.SaveDraftAsync(new()
        {
            ExpectedRevision = 0, Reason = "Brand BSworkout",
            Settings = new() { ApplicationName = "BSworkout", PrimaryColor = "#123456" }
        });
        Assert.Equal(1, draft.Revision);
        Assert.False((await service.GetPublishedAsync()).IsCustomized);
        var published = await service.PublishAsync(new() { ExpectedRevision = 1, Reason = "Approved" });
        Assert.Equal("BSworkout", published.ApplicationName);
        Assert.Equal(1, published.Version);
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() =>
            service.PublishAsync(new() { ExpectedRevision = 1, Reason = "Stale" }));
        await service.SaveDraftAsync(new()
        {
            ExpectedRevision = 2, Reason = "Second",
            Settings = new() { ApplicationName = "Other" }
        });
        await service.PublishAsync(new() { ExpectedRevision = 3, Reason = "Second publish" });
        await service.RestoreDraftAsync(new() { ExpectedRevision = 4, Version = 1, Reason = "Restore" });
        Assert.Equal("Other", (await service.GetPublishedAsync()).ApplicationName);
        await service.PublishAsync(new() { ExpectedRevision = 5, Reason = "Confirm rollback" });
        Assert.Equal("BSworkout", (await service.GetPublishedAsync()).ApplicationName);
        Assert.Equal(3, (await service.GetVersionsAsync(1,25)).Count);
        Assert.Equal(6, (await service.GetAuditAsync(1,25)).Count);
        db.UserRoles.Remove(await db.UserRoles.SingleAsync());
        await db.SaveChangesAsync();
        // Existing JWT still lists SuperAdmin but membership is gone.
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.GetDraftAsync());
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.PublishAsync(new() { ExpectedRevision = 6, Reason = "Revoked" }));
    }

    [PostgresFact]
    public async Task ProfilesAndAssetsCannotCrossAndOwnerCannotManageBranding()
    {
        await using var database = await TestDatabase.CreateAsync();
        await using var db = database.Context();
        var user = new User { Email = "admin@example.test" };
        db.Add(new UserRole { User = user, Role = new Role { Name = "SuperAdmin" } });
        var other = Guid.NewGuid();
        db.OrganizationBrandingProfiles.Add(new() { Id = other, Name = "Other" });
        var asset = new OrganizationBrandingAsset { Id = Guid.NewGuid(), ProfileId = other,
            Url = "https://assets.example.test/other.png", StorageKey = "other.png" };
        db.Add(asset);
        await db.SaveChangesAsync();
        var service = Service(db, new Current(user.Id));
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.SaveDraftAsync(new()
        {
            ExpectedRevision = 0, Reason = "Wrong profile", Settings = new() { LightLogoAssetId = asset.Id }
        }));
        await service.SaveDraftAsync(new() { ExpectedRevision = 0, Reason = "Own", Settings = new() { ApplicationName = "Own" } });
        await service.PublishAsync(new() { ExpectedRevision = 1, Reason = "Own publish" });
        Assert.False((await Service(db,new Current(user.Id),other).GetPublishedAsync()).IsCustomized);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => Service(db,new Current(user.Id,"Owner")).GetDraftAsync());
    }

    [PostgresFact]
    public async Task ConcurrentDraftUpdatesCannotOverwriteWinner()
    {
        await using var database = await TestDatabase.CreateAsync();
        await using var first = database.Context();
        var user = new User { Email = "admin@example.test" };
        first.Add(new UserRole { User = user, Role = new Role { Name = "SuperAdmin" } });
        await first.SaveChangesAsync();
        await using var second = database.Context();
        var a = Service(first,new Current(user.Id));
        var b = Service(second,new Current(user.Id));
        await a.GetDraftAsync();
        await b.GetDraftAsync();
        await a.SaveDraftAsync(new() { ExpectedRevision = 0, Reason = "Winner", Settings = new() { ApplicationName = "Winner" } });
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => b.SaveDraftAsync(new()
        { ExpectedRevision = 0, Reason = "Loser", Settings = new() { ApplicationName = "Loser" } }));
        await using var verify = database.Context();
        Assert.Single(await verify.OrganizationBrandingAudits.ToListAsync());
        Assert.Contains("Winner", (await verify.OrganizationBrandingProfiles.SingleAsync()).DraftJson);
    }
    private sealed class Storage : IObjectStorageService
    {
        public Task<StoredObjectDto> UploadAsync(string key, byte[] content, string contentType, CancellationToken ct = default) =>
            Task.FromResult(new StoredObjectDto { Key = key, Url = "https://assets.example.test/" + key });
        public Task<StoredObjectDownloadDto> DownloadAsync(string key, string? fileName = null, CancellationToken ct = default) => throw new NotSupportedException();
        public Task DeleteAsync(string key, CancellationToken ct = default) => Task.CompletedTask;
    }
}
