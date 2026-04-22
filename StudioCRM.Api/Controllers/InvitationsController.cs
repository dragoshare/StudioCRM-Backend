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
        var result = await _invitationService.CreateAsync(request);
        return Ok(result);
    }

    [HttpGet("validate")]
    [AllowAnonymous]
    public async Task<ActionResult<ValidateInvitationDto>> Validate([FromQuery] string token)
    {
        var result = await _invitationService.ValidateAsync(token);
        if (result is null) return NotFound(new { message = "Invitation is invalid or expired." });
        return Ok(result);
    }

    [HttpPost("accept")]
    [AllowAnonymous]
    public async Task<IActionResult> Accept(AcceptInvitationDto request)
    {
        var result = await _invitationService.AcceptAsync(request);
        if (!result) return BadRequest(new { message = "Invitation is invalid or expired." });
        return Ok(new { message = "Invitation accepted successfully." });
    }
}