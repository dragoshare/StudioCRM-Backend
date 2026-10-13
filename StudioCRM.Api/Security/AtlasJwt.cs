using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using StudioCRM.Application.Settings;
using StudioCRM.Infrastructure.Persistence;
using System.Security.Claims;
using System.Text;
namespace StudioCRM.Api.Security;
public static class AtlasJwt
{
    public static void Configure(JwtBearerOptions options, JwtSettings jwtSettings)
    {
    options.Events = new JwtBearerEvents
    {
        OnTokenValidated = async context =>
        {
            var rawId = context.Principal?.FindFirstValue(ClaimTypes.NameIdentifier)
                ?? context.Principal?.FindFirstValue("sub");

            var tokenEmail = context.Principal?.FindFirstValue(ClaimTypes.Email)
                ?? context.Principal?.FindFirstValue("email");
            var versionClaim = context.Principal?.FindFirstValue("auth_version");
            var tokenVersion = versionClaim is null ? 0 : int.TryParse(versionClaim, out var parsed) ? parsed : -1;
            var roles = context.Principal?.FindAll(ClaimTypes.Role).Select(c => c.Value).ToArray() ?? [];
            var organizationClaims = context.Principal?.FindAll("organization_id").ToArray() ?? [];
            var organizationId = StudioCRMDbContext.LegacyOrganizationId;
            if (organizationClaims.Length > 1 || (organizationClaims.Length == 1 &&
                (!Guid.TryParse(organizationClaims[0].Value, out organizationId) || organizationId == Guid.Empty)))
            {
                context.Fail("Organization context is unavailable.");
                return;
            }
            var factory = context.HttpContext.RequestServices.GetService<OrganizationDbContextFactory>();
            // This independent context must not freeze the request context before validation succeeds.
            await using var probe = factory?.Create(organizationId);
            var db = probe ?? context.HttpContext.RequestServices.GetRequiredService<StudioCRMDbContext>();
            if (db.OrganizationId != organizationId)
            {
                context.Fail("Organization context is unavailable.");
                return;
            }
            if (!int.TryParse(rawId, out var userId) ||
                !await db.Users.AnyAsync(u => u.Id == userId && u.IsActive && u.Email == tokenEmail && u.AuthVersion == tokenVersion &&
                    !db.Clients.IncludingOrganizationArchive(db.OrganizationId).Any(c => c.UserId == u.Id && (c.IsDeleted || c.PortalAccessBlocked))))
            {
                context.Fail("Account is inactive or client portal access is blocked.");
                return;
            }
            if (!await StudioCRM.Infrastructure.Services.AccountOrganizationResolver.MatchesContextAsync(db, userId))
            {
                context.Fail("Account organization is unavailable or ambiguous.");
                return;
            }
            var allowedRoles = await StudioCRM.Infrastructure.Services.OrganizationAuthorization.RolesAsync(db, userId);
            if (roles.Length == 0 || roles.Any(role => !allowedRoles.Contains(role)) ||
                (roles.Contains("SuperAdmin") && organizationClaims.Length != 0))
            {
                context.Fail("Roles are not available in this organization.");
                return;
            }
            context.HttpContext.RequestServices.GetService<RequestOrganizationContext>()?.SetValidated(organizationId);
        }
    };
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ClockSkew = TimeSpan.FromSeconds(30),
        ValidAlgorithms = new[] { SecurityAlgorithms.HmacSha256 },
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = jwtSettings.Issuer,
        ValidAudience = jwtSettings.Audience,
        IssuerSigningKey = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(jwtSettings.Key)),
        RoleClaimType = ClaimTypes.Role,
        NameClaimType = ClaimTypes.Name
    };
    }
}
