using Microsoft.AspNetCore.Mvc;
using StudioCRM.Api.Controllers;
using StudioCRM.Application.Common;
using StudioCRM.Application.DTOs.SessionParticipants;
using StudioCRM.Application.Interfaces;

namespace StudioCRM.Tests.UnitTests;

public class SessionCorrectionErrorTests
{
    [Fact]
    public void ErrorCodesComeFromCodedRejectionsAndThePaidSettlementMessage()
    {
        Assert.Equal(SessionCorrectionErrorCodes.RenewalAlreadyUsed,
            CodedInvalidOperationException.CodeOf(new CodedInvalidOperationException(SessionCorrectionErrorCodes.RenewalAlreadyUsed, "x")));
        Assert.Equal(SessionCorrectionErrorCodes.TrainerSettlementPaid,
            CodedInvalidOperationException.CodeOf(new InvalidOperationException(CodedInvalidOperationException.PaidSettlementMessage)));
        Assert.Null(CodedInvalidOperationException.CodeOf(new InvalidOperationException("other")));
    }

    [Fact]
    public async Task CompleteRejectsUnknownAttendanceBeforeTouchingTheSession()
    {
        var calls = 0;
        var service = OwnerPanelContractTests.Fake<ISessionParticipantService>.Create((_, _) => { calls++; return Task.FromResult(true); });
        var controller = new SessionParticipantsController(service);

        var rejected = Assert.IsType<BadRequestObjectResult>(await controller.CompleteSession(1, new CompleteSessionDto
        {
            ActualSessionType = "OneToOne",
            Participants = [new CompleteSessionParticipantDto { ClientId = 5, AttendanceStatus = "Absent" }]
        }));
        Assert.Contains(SessionCorrectionErrorCodes.InvalidAttendanceStatus, System.Text.Json.JsonSerializer.Serialize(rejected.Value));
        Assert.Equal(0, calls);

        Assert.IsType<OkObjectResult>(await controller.CompleteSession(1, new CompleteSessionDto
        {
            ActualSessionType = "OneToOne",
            Participants = [new CompleteSessionParticipantDto { ClientId = 5, AttendanceStatus = "CancelledLate" }]
        }));
        Assert.Equal(1, calls);
    }
}
