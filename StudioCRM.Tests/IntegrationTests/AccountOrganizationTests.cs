using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using StudioCRM.Domain.Entities;
using StudioCRM.Infrastructure.Persistence;
using StudioCRM.Infrastructure.Services;

namespace StudioCRM.Tests.IntegrationTests;

public class AccountOrganizationTests
{
    [PostgresFact]
    public async Task ResolvesUniqueStudioAndRejectsDraftConflictsAndArchivedForeignProfiles()
    {
        await using var test = await TestDatabase.CreateAsync();
        await using var db = test.Context();
        var profile = new OrganizationBrandingProfile { Id = Guid.NewGuid(), Name = "Demo" };
        var org = new Organization { Id = Guid.NewGuid(), Name = "Demo", Slug = "demo", Status = "Draft", BrandingProfileId = profile.Id };
        var user = new User { Email = "demo@atlas.test" };
        db.AddRange(profile, org, user);
        await db.SaveChangesAsync();
        var membership = new OrganizationMembership { OrganizationId = org.Id, UserId = user.Id, IsOwner = true };
        db.Add(membership); await db.SaveChangesAsync();
        var draft = await AccountOrganizationResolver.ResolveAsync(db, user.Id);
        Assert.Equal(org.Id, draft.OrganizationId);
        Assert.False(draft.CanAuthenticate);
        org.Status = "Active"; await db.SaveChangesAsync();
        Assert.True((await AccountOrganizationResolver.ResolveAsync(db, user.Id)).CanAuthenticate);
        Assert.False(await AccountOrganizationResolver.MatchesContextAsync(db, user.Id));
        await using var demo = test.Context(organizationId: org.Id);
        Assert.True(await AccountOrganizationResolver.MatchesContextAsync(demo, user.Id));
        var foreign = new OrganizationMembership { OrganizationId = StudioCRMDbContext.LegacyOrganizationId, UserId = user.Id, IsTrainer = true };
        db.Add(foreign); await db.SaveChangesAsync();
        Assert.False((await AccountOrganizationResolver.ResolveAsync(db, user.Id)).CanAuthenticate);
        db.Remove(foreign); await db.SaveChangesAsync();
        // An archived profile still binds an account; hiding it must not permit cross-studio access.
        db.Add(new Trainer { UserId = user.Id, IsDeleted = true });
        await db.SaveChangesAsync();
        Assert.False((await AccountOrganizationResolver.ResolveAsync(demo, user.Id)).CanAuthenticate);
    }

    [PostgresFact]
    public async Task AccountWithoutStudioCannotLoginOrRotateRefreshToken()
    {
        await using var test = await TestDatabase.CreateAsync();
        await using var db = test.Context();
        var user = new User { Email = "unassigned@atlas.test" };
        user.PasswordHash = new PasswordHasher<User>().HashPassword(user, "Test-password-123!");
        var token = new RefreshToken { User = user, Token = Guid.NewGuid().ToString(), ExpiresAt = DateTime.UtcNow.AddDays(1) };
        db.Add(token); await db.SaveChangesAsync();
        var auth = PortalSecurityTests.Auth(db);
        Assert.Null(await auth.LoginAsync(new() { Email = user.Email, Password = "Test-password-123!" }));
        Assert.Null(user.LastLoginAt);
        Assert.Equal(1, await db.RefreshTokens.CountAsync());
        await Assert.ThrowsAsync<InvalidOperationException>(() => auth.RefreshAsync(new() { RefreshToken = token.Token }));
        await db.Entry(token).ReloadAsync();
        Assert.Null(token.RevokedAt);
        Assert.Equal(1, await db.RefreshTokens.CountAsync());
    }
}
