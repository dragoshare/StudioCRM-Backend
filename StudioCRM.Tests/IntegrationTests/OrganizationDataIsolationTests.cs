using Microsoft.EntityFrameworkCore;
using Npgsql;
using StudioCRM.Domain.Entities;
using StudioCRM.Infrastructure.Persistence;

namespace StudioCRM.Tests.IntegrationTests;

public class OrganizationDataIsolationTests
{
    private static async Task<Guid> CreateOrganization(TestDatabase test)
    {
        await using var db = test.Context();
        var profile = new OrganizationBrandingProfile { Id = Guid.NewGuid(), Name = "Demo" };
        var organization = new Organization { Id = Guid.NewGuid(), Name = "Demo", Slug = "demo", BrandingProfileId = profile.Id };
        db.Add(profile); db.Add(organization); await db.SaveChangesAsync();
        return organization.Id;
    }

    [PostgresFact]
    public async Task CachedModelAndArchiveQueriesKeepEachOrganizationsDataAndAuditSeparate()
    {
        await using var test = await TestDatabase.CreateAsync();
        var demo = await CreateOrganization(test);
        foreach (var id in new[] { StudioCRMDbContext.LegacyOrganizationId, demo })
        {
            await using var db = test.Context(organizationId: id);
            var row = db.Add(new Package { Name = id.ToString() });
            Assert.Equal(id, row.Property<Guid>("OrganizationId").CurrentValue);
            await db.SaveChangesAsync();
        }
        foreach (var id in new[] { demo, StudioCRMDbContext.LegacyOrganizationId, demo })
        {
            await using var db = test.Context(organizationId: id);
            Assert.Equal(id, await db.Packages.IgnoreQueryFilters().Where(x => x.Name == id.ToString()).Select(x => EF.Property<Guid>(x, "OrganizationId")).SingleAsync());
            Assert.Equal(id.ToString(), (await db.Packages.SingleAsync()).Name);
            Assert.Equal(id.ToString(), (await db.Packages.IncludingOrganizationArchive(id).SingleAsync()).Name);
            var audit = await db.ActivityLogEntries.Where(x => x.EntityType == "Package").ToListAsync();
            Assert.Single(audit);
            Assert.Contains(id.ToString(), audit[0].AfterJson!);
        }
    }

    [PostgresFact]
    public async Task DetachedForeignRowCannotBeUpdatedOrDeletedEvenWithForgedShadowOrganization()
    {
        await using var test = await TestDatabase.CreateAsync();
        var demo = await CreateOrganization(test);
        int locationId;
        await using (var db = test.Context())
        {
            var location = new Location { Name = "Original" }; db.Add(location);
            await db.SaveChangesAsync(); locationId = location.Id;
        }
        foreach (var delete in new[] { false, true })
        {
            await using var db = test.Context(organizationId: demo);
            var entry = db.Attach(new Location { Id = locationId, Name = "Forged" });
            entry.Property("OrganizationId").CurrentValue = demo;
            entry.Property("OrganizationId").OriginalValue = demo;
            entry.State = delete ? EntityState.Deleted : EntityState.Modified;
            await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => db.SaveChangesAsync());
        }
        await using var verify = test.Context();
        Assert.Equal("Original", (await verify.Locations.SingleAsync()).Name);
    }

    [PostgresFact]
    public async Task DatabaseRejectsCrossOrganizationRelationship()
    {
        await using var test = await TestDatabase.CreateAsync();
        var demo = await CreateOrganization(test);
        int legalId;
        await using (var db = test.Context())
        {
            var legal = new LegalEntity { Name = "BS" }; db.Add(legal);
            await db.SaveChangesAsync(); legalId = legal.Id;
        }
        await using var other = test.Context(organizationId: demo);
        other.Add(new Location { Name = "Invalid link", LegalEntityId = legalId });
        var error = await Assert.ThrowsAsync<DbUpdateException>(() => other.SaveChangesAsync());
        Assert.Equal(PostgresErrorCodes.ForeignKeyViolation, Assert.IsType<PostgresException>(error.InnerException).SqlState);
    }
    [PostgresFact]
    public async Task ArchivedPackagesRemainScopedAndExplicitForeignInsertIsRejected()
    {
        await using var test = await TestDatabase.CreateAsync();
        var demo = await CreateOrganization(test);
        foreach (var id in new[] { StudioCRMDbContext.LegacyOrganizationId, demo })
        {
            await using var db = test.Context(organizationId: id);
            db.Add(new Package { Name = id.ToString(), IsDeleted = true });
            await db.SaveChangesAsync();
        }
        await using var other = test.Context(organizationId: demo);
        Assert.Empty(await other.Packages.ToListAsync());
        Assert.Equal(demo.ToString(), (await other.Packages.IncludingOrganizationArchive(demo).SingleAsync()).Name);
        var entry = other.Add(new Package { Name = "Foreign insert" });
        entry.Property("OrganizationId").CurrentValue = StudioCRMDbContext.LegacyOrganizationId;
        await Assert.ThrowsAsync<InvalidOperationException>(() => other.SaveChangesAsync());
    }}
