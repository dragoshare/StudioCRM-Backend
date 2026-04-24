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
}