using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StudioCRM.Application.DTOs.Clients;
using StudioCRM.Application.Interfaces;

namespace StudioCRM.Api.Controllers;

[ApiController]
[Route("api/client-email-changes")]
public class ClientEmailChangesController(IClientEmailChangeService service) : ControllerBase
{
    [HttpGet]
    [Authorize(Roles = "Owner,Client")]
    public async Task<IActionResult> Get(int? clientId) => Ok(await service.GetAsync(clientId));

    [HttpPost("{id:int}/review")]
    [Authorize(Roles = "Owner")]
    public async Task<IActionResult> Review(int id, ReviewClientEmailChangeRequest request)
    {
        await service.ReviewAsync(id, request);
        return NoContent();
    }

    [HttpPost("verify")]
    [AllowAnonymous]
    public async Task<IActionResult> Verify(VerifyClientEmailChangeRequest request)
    {
        await service.VerifyAsync(request);
        return Ok(new { message = "Login email changed. Sign in again using the new address." });
    }

    [HttpPost("{id:int}/cancel")]
    [Authorize(Roles = "Client")]
    public async Task<IActionResult> Cancel(int id)
    {
        await service.CancelOwnAsync(id);
        return NoContent();
    }
}
