using System.Reflection;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using StudioCRM.Api.Controllers;
using StudioCRM.Api.Security;
using StudioCRM.Application.Settings;
using StudioCRM.Domain.Entities;
using StudioCRM.Infrastructure.Persistence;
using StudioCRM.Infrastructure.Services;

namespace StudioCRM.Tests.IntegrationTests;

public class OrganizationRequestContextTests
{
    [PostgresFact]
    public async Task DemoLoginRefreshAndValidatedRequestUseDemoWithoutChangingLegacyContext()
    {
        await using var test = await TestDatabase.CreateAsync();
        await using var db = test.Context();
        var profile = new OrganizationBrandingProfile { Id = Guid.NewGuid(), Name = "Demo" };
        var org = new Organization { Id = Guid.NewGuid(), Name = "Demo", Slug = "demo", Status = "Active", BrandingProfileId = profile.Id };
        var user = new User { Email = "demo-owner@atlas.test", EmailVerifiedAt = DateTime.UtcNow };
        user.PasswordHash = new PasswordHasher<User>().HashPassword(user, "Local-test-password-123!");
        db.AddRange(profile, org, user, new Location { Name = "Legacy only" });
        await db.SaveChangesAsync();
        db.Add(new OrganizationMembership { OrganizationId = org.Id, UserId = user.Id, IsOwner = true });
        await db.SaveChangesAsync();
        var options = (DbContextOptions<StudioCRMDbContext>)db.GetService<IDbContextOptions>();
        var factory = new OrganizationDbContextFactory(options);
        var auth = new AuthService(db, Options.Create(new JwtSettings { Key = new string('k', 64), ExpiryInMinutes = 15 }),
            Options.Create(new AppSettings()), null!, NullLogger<AuthService>.Instance, factory);
        var login = await auth.LoginAsync(new() { Email = user.Email, Password = "Local-test-password-123!" });
        Assert.NotNull(login);
        Assert.Equal(org.Id, login.OrganizationId);
        Assert.Equal("Owner", login.Role);
        var refresh = await auth.RefreshAsync(new() { RefreshToken = login.RefreshToken });
        Assert.Equal(org.Id, refresh.OrganizationId);
        Assert.Equal(StudioCRMDbContext.LegacyOrganizationId, db.OrganizationId);

        var services = new ServiceCollection().AddSingleton(factory).AddScoped<RequestOrganizationContext>()
            .AddScoped<StudioCRMDbContext>(sp => factory.Create(sp.GetRequiredService<RequestOrganizationContext>().Freeze()));
        await using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var jwtOptions = new JwtBearerOptions();
        AtlasJwt.Configure(jwtOptions, new JwtSettings { Key = new string('k', 64) });
        var http = new DefaultHttpContext { RequestServices = scope.ServiceProvider };
        var claims = new[] { new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()), new Claim(ClaimTypes.Email, user.Email),
            new Claim(ClaimTypes.Role, "Owner"), new Claim("auth_version", user.AuthVersion.ToString()), new Claim("organization_id", org.Id.ToString()) };
        var validation = new TokenValidatedContext(http, new AuthenticationScheme("Bearer", null, typeof(JwtBearerHandler)), jwtOptions)
            { Principal = new ClaimsPrincipal(new ClaimsIdentity(claims, "Bearer")) };
        await jwtOptions.Events.OnTokenValidated(validation);
        Assert.Null(validation.Result?.Failure);
        var scopedDb = scope.ServiceProvider.GetRequiredService<StudioCRMDbContext>();
        Assert.Equal(org.Id, scopedDb.OrganizationId);
        Assert.Empty(await scopedDb.Locations.ToListAsync());
        var me = await PortalSecurityTests.Auth(scopedDb).GetMeAsync(user.Id);
        Assert.Equal(org.Id, me!.OrganizationId);
        Assert.Equal(new[] { "Owner" }, me.Roles);
        var location = await new LocationService(scopedDb).CreateAsync(new() { Name = "Demo only", City = "Test" });
        Assert.False(await db.Locations.AnyAsync(l => l.Id == location.Id));
        Assert.Throws<InvalidOperationException>(() => scope.ServiceProvider.GetRequiredService<RequestOrganizationContext>()
            .SetValidated(StudioCRMDbContext.LegacyOrganizationId));

        org.Status = "Draft"; await db.SaveChangesAsync();
        await Assert.ThrowsAsync<InvalidOperationException>(() => auth.RefreshAsync(new() { RefreshToken = refresh.RefreshToken }));
        using var rejectedScope = provider.CreateScope();
        var rejected = new TokenValidatedContext(new DefaultHttpContext { RequestServices = rejectedScope.ServiceProvider },
            new AuthenticationScheme("Bearer", null, typeof(JwtBearerHandler)), jwtOptions) { Principal = validation.Principal };
        await jwtOptions.Events.OnTokenValidated(rejected);
        Assert.NotNull(rejected.Result?.Failure);
        Assert.Equal(StudioCRMDbContext.LegacyOrganizationId, rejectedScope.ServiceProvider.GetRequiredService<RequestOrganizationContext>().OrganizationId);
    }

    [Theory]
    [InlineData(typeof(LocationsController), "GetAll", true)]
    [InlineData(typeof(AuthController), "Me", true)]
    [InlineData(typeof(AuthController), "Register", false)]
    [InlineData(typeof(AuthController), "UploadMyAvatar", false)]
    [InlineData(typeof(PlatformOrganizationsController), "List", false)]
    public async Task NewStudioAllowsOnlyAuditedActions(Type controller, string actionName, bool allowed)
    {
        var http = new DefaultHttpContext();
        http.Response.Body = new MemoryStream();
        http.User = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(ClaimTypes.Role, "Owner") }, "Bearer"));
        http.SetEndpoint(new Endpoint(_ => Task.CompletedTask, new EndpointMetadataCollection(new ControllerActionDescriptor
            { ControllerTypeInfo = controller.GetTypeInfo(), ActionName = actionName }), "test"));
        var scope = new RequestOrganizationContext();
        scope.SetValidated(Guid.NewGuid());
        var invoked = false;
        await new OrganizationCapabilityMiddleware(_ => { invoked = true; return Task.CompletedTask; }).InvokeAsync(http, scope);
        Assert.Equal(allowed, invoked);
        Assert.Equal(allowed ? 200 : 403, http.Response.StatusCode);
    }
}
