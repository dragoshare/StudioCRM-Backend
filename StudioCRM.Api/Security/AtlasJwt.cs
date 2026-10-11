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
            var db = context.HttpContext.RequestServices.GetRequiredService<StudioCRMDbContext>();
            var tokenEmail = context.Principal?.FindFirstValue(ClaimTypes.Email)
                ?? context.Principal?.FindFirstValue("email");
            var versionClaim = context.Principal?.FindFirstValue("auth_version");
            var tokenVersion = versionClaim is null ? 0 : int.TryParse(versionClaim, out var parsed) ? parsed : -1;
            var roles = context.Principal?.FindAll(ClaimTypes.Role).Select(c => c.Value).ToArray() ?? [];
            if (!int.TryParse(rawId, out var userId) ||
                !await db.Users.AnyAsync(u => u.Id == userId && u.IsActive && u.Email == tokenEmail && u.AuthVersion == tokenVersion &&
                    roles.All(role => u.UserRoles.Any(r => r.Role.Name == role)) &&
                    !db.Clients.IgnoreQueryFilters().Any(c => c.UserId == u.Id && (c.IsDeleted || c.PortalAccessBlocked))))
                context.Fail("Account is inactive or client portal access is blocked.");
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
