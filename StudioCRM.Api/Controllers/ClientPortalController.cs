using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StudioCRM.Application.DTOs.ClientPortal;
using StudioCRM.Application.Interfaces;

namespace StudioCRM.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Client")]
public class ClientPortalController : ControllerBase
{
    private readonly IClientPortalService _clientPortalService;

    public ClientPortalController(IClientPortalService clientPortalService)
    {
        _clientPortalService = clientPortalService;
    }

    [HttpGet("me")]
    public async Task<ActionResult<ClientPortalMeDto>> GetMe()
    {
        var result = await _clientPortalService.GetMeAsync();

        if (result is null)
            return NotFound();

        return Ok(result);
    }

    [HttpGet("dashboard")]
    public async Task<ActionResult<ClientPortalDashboardDto>> GetDashboard()
    {
        var result = await _clientPortalService.GetDashboardAsync();

        if (result is null)
            return NotFound();

        return Ok(result);
    }

    [HttpGet("schedule")]
    public async Task<ActionResult<List<ClientPortalSessionDto>>> GetSchedule()
    {
        return Ok(await _clientPortalService.GetScheduleAsync());
    }

    [HttpGet("package")]
    public async Task<ActionResult<ClientPortalPackageDto>> GetPackage()
    {
        var result = await _clientPortalService.GetPackageAsync();

        if (result is null)
            return NotFound();

        return Ok(result);
    }

    [HttpGet("payments")]
    public async Task<ActionResult<ClientPortalPaymentDto>> GetPayments()
    {
        var result = await _clientPortalService.GetPaymentAsync();

        if (result is null)
            return NotFound();

        return Ok(result);
    }

    [HttpGet("trainer")]
    public async Task<ActionResult<ClientPortalTrainerDto>> GetTrainer()
    {
        var result = await _clientPortalService.GetTrainerAsync();

        if (result is null)
            return NotFound();

        return Ok(result);
    }
    [HttpGet("package-settlement")]
    public async Task<ActionResult<ClientPackageSettlementDto>> GetPackageSettlement()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (string.IsNullOrEmpty(userId))
            return Unauthorized();

        var result = await _clientPortalService.GetPackageSettlementAsync(userId);

        return Ok(result);
    }

    [HttpGet("trainer-contact")]
    [Authorize(Roles = "Client")]
    public async Task<IActionResult> GetTrainerContact()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        if (string.IsNullOrWhiteSpace(userIdClaim))
            return Unauthorized();

        if (!int.TryParse(userIdClaim, out var userId))
            return Unauthorized();

        var contact = await _clientPortalService.GetTrainerContactAsync(userId);

        if (contact == null)
            return NotFound(new
            {
                message = "Brak przypisanego trenera dla tego klienta."
            });

        return Ok(contact);
    }
}