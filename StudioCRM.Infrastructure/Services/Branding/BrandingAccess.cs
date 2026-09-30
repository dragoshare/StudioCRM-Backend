using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using StudioCRM.Application.Interfaces;
using StudioCRM.Application.Settings;
using StudioCRM.Infrastructure.Persistence;
namespace StudioCRM.Infrastructure.Services.Branding;

public class BrandingProfileContext(IOptions<BrandingOptions> options) : IBrandingProfileContext
{
    public Guid ProfileId => options.Value.ProfileId;
}
public class BrandingAccess(StudioCRMDbContext db, ICurrentUserService current)
{
    public async Task<int> RequireAdminAsync()
    {
        if (!current.IsAuthenticated || !current.Roles.Contains("SuperAdmin") ||
            current.UserId is not int id ||
            !await db.Users.AnyAsync(u => u.Id == id && u.IsActive &&
                u.UserRoles.Any(r => r.Role.Name == "SuperAdmin")))
            throw new UnauthorizedAccessException("SuperAdmin access is required.");
        return id;
    }
}
