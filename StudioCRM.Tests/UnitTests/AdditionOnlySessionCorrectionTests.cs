using StudioCRM.Application.DTOs.SessionParticipants;
using StudioCRM.Domain.Entities;
using StudioCRM.Infrastructure.Services;

namespace StudioCRM.Tests.UnitTests;

public class AdditionOnlySessionCorrectionTests
{
    private static Session CompletedSession() => new()
    {
        Status = "Completed",
        Participants =
        [
            new SessionParticipant { ClientId = 1, AttendanceStatus = "Present", CountsAgainstPackage = true, SessionsCharged = 1, IsCountedFromPackage = true },
            new SessionParticipant { ClientId = 2, AttendanceStatus = "Present", CountsAgainstPackage = true, SessionsCharged = 1, IsCountedFromPackage = true }
        ]
    };

    private static CompleteSessionParticipantDto Present(int clientId, int sessionsCharged = 1) =>
        new() { ClientId = clientId, AttendanceStatus = "Present", CountsAgainstPackage = true, SessionsCharged = sessionsCharged };

    [Fact]
    public void AddingParticipantWithoutChangingOthersIsAdditionOnly()
    {
        var request = new CompleteSessionDto { ActualSessionType = "ThreeToOne", Participants = [Present(1), Present(2), Present(3)] };

        Assert.True(SessionParticipantService.IsAdditionOnlyCorrection(CompletedSession(), request));
    }

    [Fact]
    public void RemovingOrChangingExistingParticipantIsNotAdditionOnly()
    {
        var removed = new CompleteSessionDto { ActualSessionType = "TwoToOne", Participants = [Present(1), Present(3)] };
        var changed = new CompleteSessionDto { ActualSessionType = "ThreeToOne", Participants = [Present(1), Present(2, 2), Present(3)] };
        var unchanged = new CompleteSessionDto { ActualSessionType = "TwoToOne", Participants = [Present(1), Present(2)] };

        Assert.False(SessionParticipantService.IsAdditionOnlyCorrection(CompletedSession(), removed));
        Assert.False(SessionParticipantService.IsAdditionOnlyCorrection(CompletedSession(), changed));
        Assert.False(SessionParticipantService.IsAdditionOnlyCorrection(CompletedSession(), unchanged));
    }
}
