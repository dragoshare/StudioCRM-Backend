using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StudioCRM.Application.DTOs.TrainerPortal;
using StudioCRM.Application.Interfaces;

namespace StudioCRM.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Trainer")]
public class TrainerPortalController : ControllerBase
{
    private readonly ITrainerPortalService _trainerPortalService;

    public TrainerPortalController(ITrainerPortalService trainerPortalService)
    {
        _trainerPortalService = trainerPortalService;
    }

    [HttpGet("me")]
    public async Task<ActionResult<TrainerPortalMeDto>> GetMe()
    {
        var result = await _trainerPortalService.GetMeAsync();
        if (result is null) return NotFound();
        return Ok(result);
    }

    [HttpGet("clients")]
    public async Task<ActionResult<List<TrainerPortalClientDto>>> GetClients()
    {
        return Ok(await _trainerPortalService.GetClientsAsync());
    }

    [HttpGet("sessions")]
    public async Task<ActionResult<List<TrainerPortalSessionDto>>> GetSessions()
    {
        return Ok(await _trainerPortalService.GetSessionsAsync());
    }

    [HttpGet("dashboard")]
    public async Task<ActionResult<TrainerPortalDashboardDto>> GetDashboard()
    {
        var result = await _trainerPortalService.GetDashboardAsync();
        if (result is null) return NotFound();
        return Ok(result);
    }
}