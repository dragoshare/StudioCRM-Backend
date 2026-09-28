using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using StudioCRM.Application.DTOs.Clients;
using StudioCRM.Application.DTOs.Invitations;
using StudioCRM.Application.Interfaces;
using StudioCRM.Application.Interfaces.Mail;
using StudioCRM.Application.Settings;
using StudioCRM.Domain.Entities;
using StudioCRM.Infrastructure.Services;
namespace StudioCRM.Tests.IntegrationTests;
public class ClientLifecycleAccountTests
{
    [PostgresFact]
    public async Task OfflineClientCanAcceptInvitationWithoutLosingHistoryOrCreatingDuplicate()
    {
        await using var database = await TestDatabase.CreateAsync();
        await using var db = database.Context();
        var location = new Location { Name = "Studio", City = "Test" };
        var owner = new User { Email = "owner@example.test" };
        db.AddRange(location, owner, new Role { Name = "Client" });
        await db.SaveChangesAsync();
        var staff = new Staff(owner.Id);
        var clients = new ClientService(db, staff, null!, null!);
        var created = await clients.CreateAsync(new CreateClientDto { FirstName = "Anna", LastName = "Nowak", LocationId = location.Id });
        Assert.Equal("", created.Email);
        Assert.Equal("", created.EmailContactUrl);
        Assert.Equal("NoAccount", created.PortalAccessStatus);
        var invitations = new InvitationService(db, staff, Options.Create(new AppSettings()), new NoMail());
        var invite = await invitations.CreateAsync(new CreateInvitationDto { ClientId = created.Id, Email = "anna@example.test", Role = "Client" });
        Assert.Equal("Invited", (await clients.GetByIdAsync(created.Id))!.PortalAccessStatus);
        Assert.True(await invitations.AcceptAsync(new AcceptInvitationDto { Token = invite.Token, FirstName = "Changed", LastName = "Name", Password = "test-password-long" }));
        Assert.Equal(1, await db.Clients.CountAsync());
        var client = await db.Clients.SingleAsync();
        Assert.Equal(created.Id, client.Id);
        Assert.Equal("Anna", client.FirstName);
        Assert.NotNull(client.UserId);
        Assert.Single(await db.ClientLocationMemberships.Where(m => m.ClientId == client.Id).ToListAsync());
        await clients.SetPortalAccessAsync(client.Id, true);
        Assert.True(await ClientAccountAccess.IsBlockedAsync(db, client.UserId!.Value));
        await clients.SetPortalAccessAsync(client.Id, false);
        Assert.False(await ClientAccountAccess.IsBlockedAsync(db, client.UserId.Value));
        await clients.DeleteAsync(client.Id);
        Assert.True(await ClientAccountAccess.IsBlockedAsync(db, client.UserId.Value));
        Assert.Empty(await db.Clients.ToListAsync());
        Assert.Single(await clients.GetDeletedAsync());
        await clients.RestoreAsync(client.Id);
        Assert.False(await ClientAccountAccess.IsBlockedAsync(db, client.UserId.Value));
    }

    [PostgresFact]
    public async Task ArchiveRejectsFutureSessionAndDoesNotCancelIt()
    {
        await using var database = await TestDatabase.CreateAsync();
        await using var db = database.Context();
        var location = new Location { Name = "Studio", City = "Test" };
        var trainer = new Trainer { User = new User { Email = "trainer@example.test" } };
        var client = new Client { FirstName = "Anna", LastName = "Nowak", Location = location };
        var session = new Session { Trainer = trainer, Location = location, StartAt = DateTime.UtcNow.AddDays(1), EndAt = DateTime.UtcNow.AddDays(1).AddHours(1), Status = "Planned" };
        db.AddRange(client, new SessionParticipant { Client = client, Session = session });
        await db.SaveChangesAsync();
        var service = new ClientService(db, new Staff(trainer.UserId), null!, null!);
        Assert.Contains("FutureSessions", (await service.CheckArchiveAsync(client.Id)).Blockers);
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.DeleteAsync(client.Id));
        Assert.False(client.IsDeleted);
        Assert.Equal("Planned", session.Status);
    }

    private sealed class NoMail : IEmailService
    {
        public Task SendInvitationEmailAsync(string toEmail, string role, string locationName, string inviteLink) => Task.CompletedTask;
        public Task SendPasswordResetEmailAsync(string toEmail, string resetLink) => Task.CompletedTask;
        public Task SendEmailVerificationAsync(string toEmail, string verificationLink) => Task.CompletedTask;
    }
    private sealed class Staff(int id) : ICurrentUserService
    {
        public int? UserId => id;
        public string? Email => null;
        public List<string> Roles => new() { "Owner" };
        public bool IsAuthenticated => true;
        public bool IsOwner => true;
        public bool IsTrainer => false;
        public bool IsClient => false;
    }
}
