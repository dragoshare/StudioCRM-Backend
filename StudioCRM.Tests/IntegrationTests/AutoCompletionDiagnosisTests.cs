using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using StudioCRM.Application.DTOs.SessionParticipants;
using StudioCRM.Domain.Entities;
using StudioCRM.Domain.Enums;
using StudioCRM.Infrastructure.Services;

namespace StudioCRM.Tests.IntegrationTests;

public class AutoCompletionDiagnosisTests
{
    // Regression: failed accounting must not leak through a later save in the same context.
    [PostgresFact]
    public async Task FailedCompletionDoesNotLeakPackageUsageIntoNextSuccessfulSession()
    {
        await using var database = await TestDatabase.CreateAsync();
        await using var db = database.Context();
        var location = new Location { Name = "Diagnostic studio" };
        var trainer = new Trainer { User = new User { Email = "diagnostic@example.test" } };
        var client = new Client { FirstName = "Has package", Location = location };
        var missing = new Client { FirstName = "No package", Location = location };
        var other = new Client { FirstName = "Next session", Location = location };
        var package = new Package { Name = "One entry", BillingType = SessionBillingType.TwoToOne, Price = 100, SessionsLimit = 1 };
        var cycle = new ClientPackage { Client = client, Package = package, TotalSessions = 1,
            ExpectedBillingType = SessionBillingType.TwoToOne, ExpectedUnitPrice = 100, IsActive = true,
            PurchaseDate = DateTime.UtcNow };
        var failed = new Session { Trainer = trainer, Location = location, StartAt = DateTime.UtcNow.AddHours(-4), EndAt = DateTime.UtcNow.AddHours(-3) };
        var next = new Session { Trainer = trainer, Location = location, StartAt = DateTime.UtcNow.AddHours(-2), EndAt = DateTime.UtcNow.AddHours(-1) };
        db.AddRange(cycle, new SessionParticipant { Session = failed, Client = client },
            new SessionParticipant { Session = failed, Client = missing },
            new SessionParticipant { Session = next, Client = other });
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        var service = new SessionParticipantService(db, null!, null!, null!, NullLogger<SessionParticipantService>.Instance);
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => service.CompleteSessionAutomaticallyAsync(failed.Id,
            new CompleteSessionDto { ActualSessionType = "TwoToOne", Participants = new()
            {
                new() { ClientId = client.Id, AttendanceStatus = "Present", CountsAgainstPackage = true, SessionsCharged = 1 },
                new() { ClientId = missing.Id, AttendanceStatus = "Present", CountsAgainstPackage = true, SessionsCharged = 1 }
            }}));
        Assert.Contains("does not have an active subscription package", error.Message);
        await using (var check = database.Context())
            Assert.Equal(0, (await check.ClientPackages.SingleAsync()).UsedSessions);

        await service.CompleteSessionAutomaticallyAsync(next.Id, new CompleteSessionDto
        {
            ActualSessionType = "OneToOne", Participants = new()
            { new() { ClientId = other.Id, AttendanceStatus = "Present", CountsAgainstPackage = false, SessionsCharged = 1 } }
        });
        await using var final = database.Context();
        Assert.Equal(0, (await final.ClientPackages.SingleAsync()).UsedSessions);
        var stored = await final.Sessions.SingleAsync(s => s.Id == failed.Id);
        Assert.Equal("Planned", stored.Status);
        Assert.Null(stored.CompletedAt);
        Assert.True((await final.ClientPackages.SingleAsync()).IsActive);
        Assert.All(await final.SessionParticipants.Where(p => p.SessionId == failed.Id).ToListAsync(), p =>
        {
            Assert.False(p.IsCountedFromPackage);
            Assert.Equal("Planned", p.AttendanceStatus);
        });
        Assert.Equal("Completed", (await final.Sessions.SingleAsync(s => s.Id == next.Id)).Status);
    }
}
