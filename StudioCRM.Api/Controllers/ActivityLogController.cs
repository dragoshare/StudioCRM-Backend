using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StudioCRM.Application.DTOs.ActivityLog;
using StudioCRM.Application.DTOs.Billing;
using StudioCRM.Application.Interfaces;

namespace StudioCRM.Api.Controllers;

[ApiController]
[Route("api/activity-log")]
[Authorize(Roles = "Owner")]
public class ActivityLogController(IActivityLogService service) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<PagedResultDto<ActivityLogDto>>> Get([FromQuery] ActivityLogFilter filter, CancellationToken cancellationToken)
    {
        try { return Ok(await service.GetAsync(filter, cancellationToken)); }
        catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
    }

    [HttpGet("metadata")]
    public ActionResult<ActivityLogMetadataDto> Metadata() => Ok(service.GetMetadata());
}
