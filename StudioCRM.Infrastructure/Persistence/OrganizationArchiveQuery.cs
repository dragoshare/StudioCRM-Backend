using Microsoft.EntityFrameworkCore;
namespace StudioCRM.Infrastructure.Persistence;

public static class OrganizationArchiveQuery
{
    // EF8 IgnoreQueryFilters disables ALL filters, not only soft deletion.
    public static IQueryable<T> IncludingOrganizationArchive<T>(this IQueryable<T> query, Guid organizationId) where T : class =>
        query.IgnoreQueryFilters().Where(row => EF.Property<Guid>(row, "OrganizationId") == organizationId);
}
