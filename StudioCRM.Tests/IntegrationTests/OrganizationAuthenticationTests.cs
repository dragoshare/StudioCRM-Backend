using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using StudioCRM.Api.Security;
using StudioCRM.Application.Settings;
using StudioCRM.Domain.Entities;
using StudioCRM.Infrastructure.Persistence;

namespace StudioCRM.Tests.IntegrationTests;

public class OrganizationAuthenticationTests
{
    [PostgresFact]
    public async Task AuthResponseAndMeExposeStudioButPlatformAdminHasNoActiveStudio()
    {
        await using var test = await TestDatabase.CreateAsync();
        await using var db = test.Context();
        foreach (var roleName in new[] { "Owner", "SuperAdmin" })
        {
            var user = new User { Email = roleName + "@atlas.test", IsActive = true };
            db.Add(new UserRole { User = user, Role = new Role { Name = roleName } });
            var refresh = Guid.NewGuid().ToString();
            db.Add(new RefreshToken { User = user, Token = refresh, ExpiresAt = DateTime.UtcNow.AddDays(1) });
            await db.SaveChangesAsync();
            var auth = PortalSecurityTests.Auth(db);
            var result = await auth.RefreshAsync(new() { RefreshToken = refresh });
            var me = await auth.GetMeAsync(user.Id);
            Guid? expected = roleName == "SuperAdmin" ? null : StudioCRMDbContext.LegacyOrganizationId;
            Assert.Equal(expected, result.OrganizationId);
            Assert.Equal(expected, me!.OrganizationId);
            var jwt = new JwtSecurityTokenHandler().ReadJwtToken(result.Token);
            Assert.Equal(expected?.ToString(), jwt.Claims.SingleOrDefault(c => c.Type == "organization_id")?.Value);
        }
    }

    [PostgresFact]
    public async Task ValidatedTokenRejectsForeignMalformedAndDuplicateOrganizationsButKeepsLegacyCompatibility()
    {
        await using var test = await TestDatabase.CreateAsync();
        await using var db = test.Context();
        var user = new User { Email = "owner@atlas.test", IsActive = true };
        db.Add(new UserRole { User = user, Role = new Role { Name = "Owner" } });
        await db.SaveChangesAsync();
        using var services = new ServiceCollection().AddSingleton(db).BuildServiceProvider();
        var options = new JwtBearerOptions();
        AtlasJwt.Configure(options, new JwtSettings { Key = new string('k', 64) });
        foreach (var values in new[] { Array.Empty<string>(), new[] { StudioCRMDbContext.LegacyOrganizationId.ToString() },
                     new[] { Guid.NewGuid().ToString() }, new[] { "invalid" },
                     new[] { StudioCRMDbContext.LegacyOrganizationId.ToString(), StudioCRMDbContext.LegacyOrganizationId.ToString() } })
        {
            var claims = new List<Claim> { new(ClaimTypes.NameIdentifier, user.Id.ToString()), new(ClaimTypes.Email, user.Email),
                new(ClaimTypes.Role, "Owner"), new("auth_version", user.AuthVersion.ToString()) };
            claims.AddRange(values.Select(x => new Claim("organization_id", x)));
            var validation = new TokenValidatedContext(new DefaultHttpContext { RequestServices = services },
                new AuthenticationScheme("Bearer", null, typeof(JwtBearerHandler)), options)
                { Principal = new ClaimsPrincipal(new ClaimsIdentity(claims, "Bearer")) };
            await options.Events.OnTokenValidated(validation);
            var accepted = values.Length == 0 || (values.Length == 1 && values[0] == StudioCRMDbContext.LegacyOrganizationId.ToString());
            Assert.Equal(!accepted, validation.Result?.Failure != null);
        }
    }
    [PostgresFact]
    public async Task RefreshKeepsStoredScopeAndRejectsForeignOrPlatformScopeWithoutRotatingToken()
    {
        await using var test = await TestDatabase.CreateAsync();
        await using var db = test.Context();
        var user = new User { Email = "refresh@atlas.test", IsActive = true };
        db.Add(new UserRole { User = user, Role = new Role { Name = "Owner" } });
        await db.SaveChangesAsync();
        foreach (var (version, org, valid) in new (int, Guid?, bool)[] {
            (0, null, true), (1, StudioCRMDbContext.LegacyOrganizationId, true),
            (1, Guid.NewGuid(), false), (1, null, false), (2, StudioCRMDbContext.LegacyOrganizationId, false),
            (0, StudioCRMDbContext.LegacyOrganizationId, false) })
        {
            var token = new RefreshToken { UserId = user.Id, Token = Guid.NewGuid().ToString(),
                ScopeVersion = version, OrganizationId = org, ExpiresAt = DateTime.UtcNow.AddDays(1) };
            db.Add(token); await db.SaveChangesAsync();
            if (valid)
            {
                var response = await PortalSecurityTests.Auth(db).RefreshAsync(new() { RefreshToken = token.Token });
                var rotated = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.SingleAsync(db.RefreshTokens, x => x.Token == response.RefreshToken);
                Assert.Equal(1, rotated.ScopeVersion);
                Assert.Equal(StudioCRMDbContext.LegacyOrganizationId, rotated.OrganizationId);
                Assert.NotNull(token.RevokedAt);
            }
            else
            {
                await Assert.ThrowsAsync<InvalidOperationException>(() => PortalSecurityTests.Auth(db).RefreshAsync(new() { RefreshToken = token.Token }));
                await db.Entry(token).ReloadAsync();
                Assert.Null(token.RevokedAt);
            }
        }
    }}