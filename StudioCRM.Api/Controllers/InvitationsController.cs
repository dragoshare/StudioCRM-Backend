using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using StudioCRM.Application.DTOs.Invitations;
using StudioCRM.Application.Interfaces;

namespace StudioCRM.Api.Controllers;

[ApiController]
[Route("api/invitations")]
public class InvitationsController : ControllerBase
{
    private readonly IInvitationService _invitationService;

    public InvitationsController(IInvitationService invitationService)
    {
        _invitationService = invitationService;
    }

    [HttpPost]
    [Authorize(Roles = "Owner,Trainer")]
    public async Task<ActionResult<InvitationDto>> Create(CreateInvitationDto request)
    {
        try
        {
            var result = await _invitationService.CreateAsync(request);
            return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (DbUpdateException ex)
        {
            return Conflict(new { message = GetDatabaseConflictMessage(ex, "saved") });
        }
    }

    [HttpGet]
    [Authorize(Roles = "Owner,Trainer")]
    public async Task<ActionResult<List<InvitationDto>>> GetAll([FromQuery] InvitationFilterDto filter)
    {
        return Ok(await _invitationService.GetAllAsync(filter));
    }

    [HttpGet("{id:int}")]
    [Authorize(Roles = "Owner,Trainer")]
    public async Task<ActionResult<InvitationDto>> GetById(int id)
    {
        var result = await _invitationService.GetByIdAsync(id);

        if (result is null)
            return NotFound();

        return Ok(result);
    }

    [HttpPost("{id:int}/resend")]
    [Authorize(Roles = "Owner,Trainer")]
    public async Task<ActionResult<InvitationDto>> Resend(int id)
    {
        try
        {
            var result = await _invitationService.ResendAsync(id);

            if (result is null)
                return NotFound();

            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (DbUpdateException ex)
        {
            return Conflict(new { message = GetDatabaseConflictMessage(ex, "resent") });
        }
    }

    [HttpPost("{id:int}/cancel")]
    [Authorize(Roles = "Owner,Trainer")]
    public async Task<IActionResult> Cancel(int id)
    {
        try
        {
            var result = await _invitationService.CancelAsync(id);

            if (!result)
                return NotFound();

            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (DbUpdateException ex)
        {
            return Conflict(new { message = GetDatabaseConflictMessage(ex, "cancelled") });
        }
    }

    [HttpDelete("{id:int}")]
    [Authorize(Roles = "Owner,Trainer")]
    public Task<IActionResult> Delete(int id)
    {
        return Cancel(id);
    }

    [HttpGet("validate")]
    [AllowAnonymous]
    public async Task<ActionResult<ValidateInvitationDto>> Validate([FromQuery] string token)
    {
        var result = await _invitationService.ValidateAsync(token);

        if (result is null)
            return NotFound(new { message = "Invitation is invalid or expired." });

        return Ok(result);
    }

    [HttpPost("accept")]
    [AllowAnonymous]
    public async Task<IActionResult> Accept(AcceptInvitationDto request)
    {
        try
        {
            var result = await _invitationService.AcceptAsync(request);

            if (!result)
                return BadRequest(new { message = "Invitation is invalid or expired." });

            return Ok(new { message = "Invitation accepted successfully." });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (DbUpdateException ex)
        {
            return Conflict(new { message = GetDatabaseConflictMessage(ex, "accepted") });
        }
    }

    private static string GetDatabaseConflictMessage(DbUpdateException exception, string action)
    {
        if (exception.InnerException is PostgresException postgresException)
        {
            return postgresException.SqlState switch
            {
                PostgresErrorCodes.UndefinedColumn =>
                    "Invitation could not be saved because the database schema is not up to date. Wait for the latest deploy to finish and try again.",
                PostgresErrorCodes.ForeignKeyViolation =>
                    "Invitation could not be saved because the selected location or trainer no longer exists. Refresh the page and try again.",
                PostgresErrorCodes.UniqueViolation =>
                    "Invitation could not be saved because of a duplicate database value. Try again.",
                _ => $"Invitation could not be {action} because of a database conflict."
            };
        }

        return $"Invitation could not be {action} because of a database conflict.";
    }
}
