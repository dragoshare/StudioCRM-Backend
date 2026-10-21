using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using StudioCRM.Application.DTOs.SessionParticipants;
using StudioCRM.Domain.Entities;
using StudioCRM.Infrastructure.Services;

namespace StudioCRM.Tests.IntegrationTests;

public class EmptyGroupClassCompletionTests
{
    // Group classes booked outside CRM: a finished group class with nobody enrolled is completed (manually and by the
    // worker) so the trainer is paid; a personal session without participants still is not.
    [PostgresFact]
    public async Task FinishedGroupClassWithoutParticipantsIsCompleted()
    {
        await using var database = await TestDatabase.CreateAsync();
        await using var db = database.Context();
        var location = new Location { Name = "Group studio" };
        var trainer = new Trainer { User = new User { Email = "empty-group@test.invalid" } };
        Session Finished(int hoursAgo, string type, string title) => new()
        {
            Trainer = trainer, Location = location, Title = title, PlannedSessionType = type,
            StartAt = DateTime.UtcNow.AddHours(-hoursAgo - 1), EndAt = DateTime.UtcNow.AddHours(-hoursAgo)
        };
        var manual = Finished(10, "Group", "TRX");
        var automatic = Finished(5, "Group", "Stretching");
        var personal = Finished(3, "OneToOne", "Pusta");
        db.AddRange(manual, automatic, personal);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        var participants = new SessionParticipantService(db, null!, null!, null!, NullLogger<SessionParticipantService>.Instance);
        Assert.True(await participants.CompleteSessionAutomaticallyAsync(manual.Id, new CompleteSessionDto { ActualSessionType = "Group" }));
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            participants.CompleteSessionAutomaticallyAsync(personal.Id, new CompleteSessionDto { ActualSessionType = "OneToOne" }));
        db.ChangeTracker.Clear();

        var worker = new SessionAutoCompletionService(db, participants, NullLogger<SessionAutoCompletionService>.Instance);
        await worker.CompleteFinishedSessionsAsync();

        await using var check = database.Context();
        var sessions = await check.Sessions.ToDictionaryAsync(s => s.Id);
        Assert.Equal(("Completed", "Group", 0), (sessions[manual.Id].Status, sessions[manual.Id].ActualSessionType, sessions[manual.Id].ActualParticipantsCount));
        Assert.Equal(("Completed", "Group"), (sessions[automatic.Id].Status, sessions[automatic.Id].ActualSessionType));
        Assert.Equal("Planned", sessions[personal.Id].Status);
    }
}
