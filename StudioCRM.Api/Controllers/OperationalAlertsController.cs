using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StudioCRM.Application.DTOs.Alerts;
using StudioCRM.Application.Interfaces;

namespace StudioCRM.Api.Controllers;

[ApiController]
[Route("api/operational-alerts")]
[Authorize(Roles = "Owner,Trainer")]
public class OperationalAlertsController : ControllerBase
{
    private readonly IOperationalAlertService _alertService;

    public OperationalAlertsController(IOperationalAlertService alertService)
    {
        _alertService = alertService;
    }

    [HttpGet]
    public async Task<ActionResult<OperationalAlertsDto>> GetAlerts(
        [FromQuery] OperationalAlertFilterDto filter)
    {
        return Ok(await _alertService.GetAlertsAsync(filter));
    }
}
