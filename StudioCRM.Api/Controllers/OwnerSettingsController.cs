using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StudioCRM.Application.DTOs.Settings;
using StudioCRM.Application.Interfaces;

namespace StudioCRM.Api.Controllers;

[ApiController]
[Route("api/owner/settings")]
[Authorize(Roles = "Owner")]
public class OwnerSettingsController : ControllerBase
{
    private readonly IStudioSettingsService _settingsService;

    public OwnerSettingsController(IStudioSettingsService settingsService)
    {
        _settingsService = settingsService;
    }

    [HttpGet]
    public async Task<ActionResult<OwnerSettingsDto>> Get()
    {
        return Ok(await _settingsService.GetOwnerSettingsAsync());
    }

    [HttpPut]
    public async Task<ActionResult<OwnerSettingsDto>> Update(UpdateOwnerSettingsDto request)
    {
        try
        {
            return Ok(await _settingsService.UpdateOwnerSettingsAsync(request));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }
}
