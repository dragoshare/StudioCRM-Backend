using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using StudioCRM.Application.Interfaces;
using StudioCRM.Application.Settings;
namespace StudioCRM.Infrastructure.Persistence;
public sealed class OrganizationDbContextFactory(DbContextOptions<StudioCRMDbContext> options,
    IOptions<OutlookSettings>? outlook = null, ICurrentUserService? currentUser = null)
{
    public StudioCRMDbContext Create(Guid organizationId) => new(options, outlook, currentUser, organizationId);
}
