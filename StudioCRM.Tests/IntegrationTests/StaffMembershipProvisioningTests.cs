using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using StudioCRM.Application.Settings;
using StudioCRM.Domain.Entities;
using StudioCRM.Infrastructure.Services;
namespace StudioCRM.Tests.IntegrationTests;
public class StaffMembershipProvisioningTests
{
    [PostgresFact]
    public async Task CreationRegistrationAndInvitationPersistMembership()
    {
        await using var test = await TestDatabase.CreateAsync();
        await using var db = test.Context();
        var location = new Location { Name = "Studio" };
        db.Add(location); db.Add(new Role { Name = "Trainer" });
        await db.SaveChangesAsync();
        await new TrainerService(db).CreateAsync(new() { Email = "direct@atlas.test", Password = "Test-password-123!", FirstName = "Direct", LastName = "Trainer", LocationIds = [location.Id] });
        await PortalSecurityTests.Auth(db).RegisterAsync(new() { Email = "register@atlas.test", Password = "Test-password-123!", FirstName = "Register", LastName = "Trainer", Role = "Trainer", LocationId = location.Id });
        var invitation = new Invitation { Email = "invite@atlas.test", Token = Guid.NewGuid().ToString(), Role = "Trainer", LocationId = location.Id, ExpiresAt = DateTime.UtcNow.AddDays(1) };
        db.Add(invitation); await db.SaveChangesAsync();
        var service = new InvitationService(db, new PortalSecurityTests.Actor(1, "Owner"), Options.Create(new AppSettings()), null!);
        Assert.True(await service.AcceptAsync(new() { Token = invitation.Token, FirstName = "Invite", LastName = "Trainer", Password = "Test-password-123!" }));
        var memberships = await db.Set<OrganizationMembership>().ToListAsync();
        Assert.Equal(3, memberships.Count);
        Assert.All(memberships, m => { Assert.True(m.IsTrainer); Assert.False(m.IsOwner); Assert.Equal(db.OrganizationId, m.OrganizationId); });
    }
    [PostgresFact]
    public async Task AddingTrainerProfilePreservesOwnerMembership()
    {
        await using var test = await TestDatabase.CreateAsync();
        await using var db = test.Context();
        var owner = new User { Email = "owner@atlas.test" };
        var location = new Location { Name = "Studio" };
        db.Add(location); db.Add(new Role { Name = "Trainer" });
        db.Add(new UserRole { User = owner, Role = new Role { Name = "Owner" } });
        await db.SaveChangesAsync();
        db.Add(new OrganizationMembership { UserId = owner.Id, OrganizationId = db.OrganizationId, IsOwner = true });
        await db.SaveChangesAsync();
        await new TrainerService(db).CreateAsync(new() { Email = owner.Email, FirstName = "Owner", LastName = "Trainer", LocationIds = [location.Id] });
        var member = await db.Set<OrganizationMembership>().SingleAsync();
        Assert.True(member.IsOwner); Assert.True(member.IsTrainer);
    }
    [PostgresFact]
    public async Task OtherStudioCannotUseGlobalRoleCreation()
    {
        await using var test = await TestDatabase.CreateAsync();
        await using var db = test.Context(organizationId: Guid.NewGuid());
        await Assert.ThrowsAsync<InvalidOperationException>(() => new TrainerService(db).CreateAsync(new() { Email = "blocked@atlas.test" }));
        await Assert.ThrowsAsync<InvalidOperationException>(() => PortalSecurityTests.Auth(db).RegisterAsync(new() { Email = "blocked@atlas.test" }));
        Assert.Empty(await db.Users.ToListAsync());
    }
    private sealed class RejectTrainerSave : Microsoft.EntityFrameworkCore.Diagnostics.SaveChangesInterceptor
    {
        public override ValueTask<Microsoft.EntityFrameworkCore.Diagnostics.InterceptionResult<int>> SavingChangesAsync(
            Microsoft.EntityFrameworkCore.Diagnostics.DbContextEventData eventData,
            Microsoft.EntityFrameworkCore.Diagnostics.InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            if (eventData.Context!.ChangeTracker.Entries<Trainer>().Any(e => e.State == EntityState.Added))
                throw new InvalidOperationException("Simulated profile storage failure");
            return base.SavingChangesAsync(eventData, result, cancellationToken);
        }
    }
    [PostgresFact]
    public async Task FailedTrainerProfileDoesNotLeaveAccountRoleOrMembership()
    {
        await using var test = await TestDatabase.CreateAsync();
        int locationId;
        await using (var seed = test.Context())
        {
            var location = new Location { Name = "Studio" };
            seed.Add(location); seed.Add(new Role { Name = "Trainer" });
            await seed.SaveChangesAsync(); locationId = location.Id;
        }
        foreach (var manualRegistration in new[] { false, true })
        {
            await using var db = test.Context(interceptor: new RejectTrainerSave());
            if (manualRegistration)
                await Assert.ThrowsAsync<InvalidOperationException>(() => PortalSecurityTests.Auth(db).RegisterAsync(new() {
                    Email = "failure@atlas.test", Password = "Test-password-123!", FirstName = "Fail", LastName = "Test", Role = "Trainer", LocationId = locationId }));
            else
                await Assert.ThrowsAsync<InvalidOperationException>(() => new TrainerService(db).CreateAsync(new() {
                    Email = "failure@atlas.test", Password = "Test-password-123!", FirstName = "Fail", LastName = "Test", LocationIds = [locationId] }));
            await using var verify = test.Context();
            Assert.Empty(await verify.Users.ToListAsync());
            Assert.Empty(await verify.UserRoles.ToListAsync());
            Assert.Empty(await verify.Set<OrganizationMembership>().ToListAsync());
        }
    }}