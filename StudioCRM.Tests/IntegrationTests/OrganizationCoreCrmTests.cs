using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using StudioCRM.Api.BackgroundServices;
using StudioCRM.Api.Security;
using StudioCRM.Application.ClientPackages.Services;
using StudioCRM.Domain.Entities;
using StudioCRM.Domain.Enums;
using StudioCRM.Infrastructure.Persistence;
using StudioCRM.Infrastructure.Services;
namespace StudioCRM.Tests.IntegrationTests;

public class OrganizationCoreCrmTests
{
    private static async Task<Guid> Studio(StudioCRMDbContext db, string status = "Active")
    {
        var profile = new OrganizationBrandingProfile { Id = Guid.NewGuid(), Name = "Demo" };
        var org = new Organization { Id = Guid.NewGuid(), Name = "Demo", Slug = Guid.NewGuid().ToString(), Status = status, BrandingProfileId = profile.Id };
        db.AddRange(profile, org); await db.SaveChangesAsync(); return org.Id;
    }
    [PostgresFact]
    public async Task SettingsPackagesPaymentsAndSessionsRemainInsideTheirStudio()
    {
        await using var test = await TestDatabase.CreateAsync(); await using var legacy = test.Context();
        var id = await Studio(legacy); await using var db = test.Context(organizationId: id);
        var location = new Location { Name = "Demo" };
        var trainer = new Trainer { User = new User { Email = "core@atlas.test" } };
        db.Add(new TrainerLocation { Trainer = trainer, Location = location });
        var client = new Client { FirstName = "Demo", LastName = "Client", Trainer = trainer, Location = location };
        db.Add(client); await db.SaveChangesAsync();
        var actor = new PortalSecurityTests.Actor(trainer.UserId, "Owner");
        var settings = new StudioSettingsService(db, actor);
        var legacySettings = new StudioSettingsService(legacy, actor);
        var before = await legacySettings.GetOwnerSettingsAsync();
        await settings.UpdateOwnerSettingsAsync(new() { DefaultPackageValidityDays = 67 });
        Assert.Equal(67, (await settings.GetOwnerSettingsAsync()).DefaultPackageValidityDays);
        Assert.Equal(before.DefaultPackageValidityDays, (await legacySettings.GetOwnerSettingsAsync()).DefaultPackageValidityDays);
        var offer = await new PackageService(db, actor).CreateAsync(new() { Name = "Demo 4", Price = 400, SessionsLimit = 4, DurationDays = 67, LocationId = location.Id });
        await Assert.ThrowsAsync<InvalidOperationException>(() => new PackageService(db, actor).CreateAsync(new() { Name = "Public", IsPubliclyAvailable = true }));
        await Assert.ThrowsAsync<InvalidOperationException>(() => PortalSecurityTests.Sessions(db, actor).CreateAsync(new() { IsPubliclyBookable = true, StartAt = DateTime.UtcNow.AddDays(1), EndAt = DateTime.UtcNow.AddDays(1).AddHours(1) }));
        var packages = new ClientPackageService(db, actor, settings);
        var passId = await packages.CreateAsync(new() { ClientId = client.Id, PackageId = offer.Id, PurchaseDate = DateTime.UtcNow });
        await PortalSecurityTests.Payments(db, actor).CreatePaymentAsStaffAsync(new() { ClientId = client.Id, ClientPackageId = passId, Amount = 400, Method = PaymentMethod.Cash });
        Assert.Equal(PaymentStatus.Paid, (await db.ClientPackages.SingleAsync(p => p.Id == passId)).PaymentStatus);
        var start = DateTime.UtcNow.AddDays(1);
        var session = await PortalSecurityTests.Sessions(db, actor).CreateAsync(new() { TrainerId = trainer.Id, LocationId = location.Id,
            StartAt = start, EndAt = start.AddHours(1), Participants = [new() { ClientId = client.Id, CountsAgainstPackage = false }] });
        await new SessionParticipantService(db, actor, null!, new PortalSecurityTests.NoSync(), NullLogger<SessionParticipantService>.Instance).CompleteSessionAsync(session.Id, new()
        { ActualSessionType = "OneToOne", Participants = [new() { ClientId = client.Id, AttendanceStatus = "Present", CountsAgainstPackage = true }] });
        Assert.Equal(1, (await db.ClientPackages.SingleAsync(p => p.Id == passId)).UsedSessions);
        Assert.False(await legacy.Sessions.AnyAsync(s => s.Id == session.Id));
        Assert.False(await legacy.Packages.AnyAsync(p => p.Id == offer.Id));
        Assert.False(await legacy.ClientPackages.AnyAsync(p => p.Id == passId));
        Assert.False(await legacy.ClientPayments.AnyAsync(p => p.ClientId == client.Id));
        await Assert.ThrowsAnyAsync<Exception>(() => new ClientPackageService(legacy, actor, legacySettings).CreateAsync(new() { ClientId = client.Id, PackageId = offer.Id }));
    }
    [PostgresFact]
    public async Task JobsVisitActiveStudiosInIndependentScopesAndContinueAfterOneFailure()
    {
        await using var test = await TestDatabase.CreateAsync(); await using var db = test.Context();
        var active = await Studio(db); var draft = await Studio(db, "Draft");
        var options = (DbContextOptions<StudioCRMDbContext>)db.GetService<IDbContextOptions>();
        var services = new ServiceCollection().AddSingleton(new OrganizationDbContextFactory(options))
            .AddScoped<RequestOrganizationContext>().AddScoped<StudioCRMDbContext>(sp => sp.GetRequiredService<OrganizationDbContextFactory>()
                .Create(sp.GetRequiredService<RequestOrganizationContext>().Freeze()));
        using var provider = services.BuildServiceProvider();
        var visited = new HashSet<Guid>();
        var runner = new OrganizationJobRunner(provider.GetRequiredService<IServiceScopeFactory>(), NullLogger<OrganizationJobRunner>.Instance);
        await runner.RunAsync((sp, ct) => { var context = sp.GetRequiredService<StudioCRMDbContext>(); visited.Add(context.OrganizationId); throw new InvalidOperationException("Simulated failure"); }, default);
        Assert.Contains(active, visited); Assert.Contains(StudioCRMDbContext.LegacyOrganizationId, visited); Assert.DoesNotContain(draft, visited);
        Assert.Equal(2, visited.Count);
    }
    [PostgresFact]
    public async Task RegisteredClientHasOnlyLocalRoleAndSeesOnlyTheirOwnerContact()
    {
        await using var test = await TestDatabase.CreateAsync(); await using var legacy = test.Context();
        var id = await Studio(legacy); await using var db = test.Context(organizationId: id);
        var location = new Location { Name = "Demo" }; var owner = new User { Email = "demo-owner@atlas.test" };
        var foreign = new User { Email = "bs-owner@atlas.test" };
        legacy.Add(new StudioCRM.Domain.Entities.UserRole { User = foreign, Role = new Role { Name = "Owner" } });
        legacy.Add(new Role { Name = "Client" }); await legacy.SaveChangesAsync();
        db.AddRange(location, owner); await db.SaveChangesAsync();
        db.Add(new OrganizationMembership { OrganizationId = id, UserId = owner.Id, IsOwner = true }); await db.SaveChangesAsync();
        var auth = PortalSecurityTests.Auth(db);
        var created = await auth.RegisterAsync(new() { Email = "local-client@atlas.test", Password = "Test-only-password-123!", FirstName = "Local", LastName = "Client", Role = "Client", LocationId = location.Id });
        Assert.Empty(await db.UserRoles.Where(r => r.UserId == created.UserId).ToListAsync());
        var login = await auth.LoginAsync(new() { Email = created.Email, Password = "Test-only-password-123!" });
        Assert.Equal(id, login!.OrganizationId); Assert.Equal("Client", login.Role);
        var portal = new ClientPortalService(db, new PortalSecurityTests.Actor(created.UserId, "Client"));
        Assert.NotNull(await portal.GetMeAsync());
        Assert.Equal(owner.Email, (await portal.GetOwnerContactAsync())!.Email);
        await NotificationWriter.QueueForOwnersAsync(db, "demo-event", "Test", "Demo only");
        await db.SaveChangesAsync();
        Assert.Equal(owner.Id, (await db.Notifications.SingleAsync()).UserId);
        Assert.Empty(await legacy.Notifications.ToListAsync());
        Assert.False(await legacy.Clients.AnyAsync(c => c.UserId == created.UserId));
    }
    [PostgresFact]
    public async Task SettingsMigrationPreservesLegacyValuesAndPermitsSameKeyInAnotherStudio()
    {
        await using var test = await TestDatabase.CreateAsync(); await using var db = test.Context();
        // Recreate the previous index on the isolated test schema, then execute the actual migration operations.
        await db.Database.ExecuteSqlRawAsync("DROP INDEX \"IX_SystemSettings_OrganizationId_Key\"; CREATE UNIQUE INDEX \"IX_SystemSettings_Key\" ON \"SystemSettings\" (\"Key\");");
        var before = await db.SystemSettings.AsNoTracking().OrderBy(x => x.Id).Select(x => x.Value).ToListAsync();
        var migration = new StudioCRM.Infrastructure.Migrations.ScopeStudioSettingKeys();
        var sql = db.GetService<Microsoft.EntityFrameworkCore.Migrations.IMigrationsSqlGenerator>().Generate(migration.UpOperations, db.Model);
        foreach (var command in sql) await db.Database.ExecuteSqlRawAsync(command.CommandText);
        Assert.Equal(before, await db.SystemSettings.AsNoTracking().OrderBy(x => x.Id).Select(x => x.Value).ToListAsync());
        var id = await Studio(db); await using var studio = test.Context(organizationId: id);
        var key = await db.SystemSettings.Select(x => x.Key).FirstAsync();
        studio.Add(new SystemSetting { Key = key, Value = "123" }); await studio.SaveChangesAsync();
        Assert.Equal("123", (await studio.SystemSettings.SingleAsync()).Value);
    }
}
