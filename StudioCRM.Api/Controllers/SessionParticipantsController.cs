using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StudioCRM.Application.DTOs.SessionParticipants;
using StudioCRM.Application.Interfaces;

namespace StudioCRM.Api.Controllers;

[ApiController]
[Route("api/sessions/{sessionId:int}/participants")]
[Authorize(Roles = "Owner")]
public class SessionParticipantsController : ControllerBase
{
    private readonly ISessionParticipantService _service;

    public SessionParticipantsController(ISessionParticipantService service)
    {
        _service = service;
    }

    [HttpGet]
    public async Task<ActionResult<List<SessionParticipantDto>>> GetBySessionId(int sessionId)
    {
        return Ok(await _service.GetBySessionIdAsync(sessionId));
    }

    [HttpPost]
    public async Task<ActionResult<SessionParticipantDto>> AddParticipant(
        int sessionId,
        AddSessionParticipantDto request)
    {
        try
        {
            return Ok(await _service.AddParticipantAsync(sessionId, request));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpDelete("{participantId:int}")]
    public async Task<IActionResult> RemoveParticipant(int sessionId, int participantId)
    {
        try
        {
            var result = await _service.RemoveParticipantAsync(sessionId, participantId);

            if (!result)
                return NotFound();

            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("~/api/sessions/{sessionId:int}/complete")]
    public async Task<IActionResult> CompleteSession(
        int sessionId,
        CompleteSessionDto request)
    {
        // Only these values have defined settlement behaviour; anything else would be stored but treated as unknown.
        var unknown = request.Participants.Select(p => p.AttendanceStatus)
            .FirstOrDefault(status => !StudioCRM.Application.Common.SessionAttendanceStatuses.Settlement.Contains(status));
        if (unknown != null)
            return BadRequest(new
            {
                message = $"Unknown attendanceStatus '{unknown}'. Allowed: {string.Join(", ", StudioCRM.Application.Common.SessionAttendanceStatuses.Settlement)}.",
                code = StudioCRM.Application.Common.SessionCorrectionErrorCodes.InvalidAttendanceStatus
            });

        try
        {
            var result = await _service.CompleteSessionAsync(sessionId, request);

            if (!result)
                return NotFound();

            return Ok(new { message = "Session completed successfully." });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(StudioCRM.Application.Common.CodedInvalidOperationException.Body(ex));
        }
    }
}
