using Microsoft.EntityFrameworkCore;
using StudioCRM.Infrastructure.Persistence;

namespace StudioCRM.Infrastructure.Services;

public static class ClientAccountAccess
{
    public static Task<bool> IsBlockedAsync(StudioCRMDbContext context, int userId) =>
        context.Clients.IgnoreQueryFilters().AnyAsync(c => c.UserId == userId && (c.PortalAccessBlocked || c.IsDeleted));
}
