using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StudioCRM.Application.DTOs.Invitations;
using StudioCRM.Application.Interfaces;

namespace StudioCRM.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class InvitationsController : ControllerBase
{
    private readonly IInvitationService _invitationService;

    public InvitationsController(IInvitationService invitationService)
    {
        _invitationService = invitationService;
    }

    [HttpPost]
    [Authorize(Roles = "Owner")]
    public async Task<ActionResult<InvitationDto>> Create(CreateInvitationDto request)
    {
        try
        {
            var result = await _invitationService.CreateAsync(request);
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpGet]
    [Authorize(Roles = "Owner")]
    public async Task<ActionResult<List<InvitationDto>>> GetAll([FromQuery] InvitationFilterDto filter)
    {
        return Ok(await _invitationService.GetAllAsync(filter));
    }

    [HttpGet("{id:int}")]
    [Authorize(Roles = "Owner")]
    public async Task<ActionResult<InvitationDto>> GetById(int id)
    {
        var result = await _invitationService.GetByIdAsync(id);

        if (result is null)
            return NotFound();

        return Ok(result);
    }

    [HttpPost("{id:int}/resend")]
    [Authorize(Roles = "Owner")]
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
    }

    [HttpPost("{id:int}/cancel")]
    [Authorize(Roles = "Owner")]
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
    }
}