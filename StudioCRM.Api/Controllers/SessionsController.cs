using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StudioCRM.Application.DTOs.Sessions;
using StudioCRM.Application.Interfaces;

namespace StudioCRM.Api.Controllers;

[ApiController]
[Route("api/sessions")]
[Authorize(Roles = "Owner,Trainer")]
public class SessionsController : ControllerBase
{
    private readonly ISessionService _sessionService;
    private readonly ILogger<SessionsController>? _logger;

    public SessionsController(ISessionService sessionService, ILogger<SessionsController>? logger = null)
    {
        _sessionService = sessionService;
        _logger = logger;
    }

    [HttpGet]
    public async Task<ActionResult<List<SessionDto>>> GetAll()
    {
        return Ok(await _sessionService.GetAllAsync());
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<SessionDto>> GetById(int id)
    {
        var result = await _sessionService.GetByIdAsync(id);
        if (result is null) return NotFound();
        return Ok(result);
    }

    [HttpGet("{id:int}/workspace")]
    public async Task<ActionResult<SessionWorkspaceDto>> GetWorkspace(int id)
    {
        return await HandleAsync<SessionWorkspaceDto>(async () =>
        {
            var result = await _sessionService.GetWorkspaceAsync(id);
            return result is null ? NotFound() : Ok(result);
        });
    }

    [HttpGet("filter")]
    [ProducesResponseType(typeof(List<SessionDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(StudioCRM.Application.DTOs.Billing.PagedResultDto<SessionDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<object>> Filter([FromQuery] SessionFilterDto filter)
    {
        // Page/PageSize switch to the paged history envelope; without them the schedule list stays unchanged.
        return await HandleAsync<object>(async () => filter.IsPaged
            ? Ok(await _sessionService.GetFilteredPageAsync(filter))
            : Ok(await _sessionService.GetFilteredAsync(filter)));
    }

    [HttpPost]
    [Authorize(Roles = "Owner")]
    public async Task<ActionResult<SessionDto>> Create(CreateSessionDto request)
    {
        return await HandleAsync<SessionDto>(async () =>
        {
            var result = await _sessionService.CreateAsync(request);
            return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
        });
    }

    [HttpGet("{id:int}/corrections")]
    [Authorize(Roles = "Owner")]
    public async Task<ActionResult<List<SessionCorrectionDto>>> GetCorrections(int id)
    {
        return Ok(await _sessionService.GetCorrectionsAsync(id));
    }

    [HttpPost("series")]
    [Authorize(Roles = "Owner")]
    public async Task<ActionResult<SessionSeriesDto>> CreateSeries(CreateSessionSeriesDto request)
    {
        return await HandleAsync<SessionSeriesDto>(async () =>
        {
            var result = await _sessionService.CreateSeriesAsync(request);
            return StatusCode(StatusCodes.Status201Created, result);
        });
    }

    [HttpPost("series/{recurringGroupId}/sync-outlook")]
    [Authorize(Roles = "Owner")]
    public async Task<ActionResult<SessionSeriesOutlookSyncDto>> SyncSeriesToOutlook(
        string recurringGroupId,
        [FromBody(EmptyBodyBehavior = Microsoft.AspNetCore.Mvc.ModelBinding.EmptyBodyBehavior.Allow)]
        SessionRecurrenceDto? recurrence)
    {
        return await HandleAsync<SessionSeriesOutlookSyncDto>(async () =>
            Ok(await _sessionService.SyncSeriesToOutlookAsync(recurringGroupId, recurrence)));
    }

    [HttpDelete("series/{recurringGroupId}")]
    [Authorize(Roles = "Owner")]
    public async Task<ActionResult<DeleteSessionSeriesResultDto>> DeleteSeries(string recurringGroupId)
    {
        return await HandleAsync<DeleteSessionSeriesResultDto>(async () =>
            Ok(await _sessionService.DeleteSeriesAsync(recurringGroupId)));
    }

    [HttpPut("{id:int}")]
    [Authorize(Roles = "Owner")]
    public async Task<ActionResult<SessionDto>> Update(int id, UpdateSessionDto request)
    {
        return await HandleAsync<SessionDto>(async () =>
        {
            var result = await _sessionService.UpdateAsync(id, request);
            return result is null ? NotFound() : Ok(result);
        });
    }

    [HttpDelete("{id:int}")]
    [Authorize(Roles = "Owner")]
    public async Task<IActionResult> Delete(int id)
    {
        var deleted = await _sessionService.DeleteAsync(id);
        if (!deleted) return NotFound();
        return NoContent();
    }
    [HttpPost("{id:int}/restore")]
    [Authorize(Roles = "Owner")]
    public async Task<IActionResult> Restore(int id)
    {
        return await HandleAsync(async () =>
        {
            var restored = await _sessionService.RestoreAsync(id);
            return restored ? NoContent() : NotFound();
        });
    }

    [HttpGet("deleted")]
    [Authorize(Roles = "Owner")]
    public async Task<ActionResult<List<SessionDto>>> GetDeleted()
    {
        return Ok(await _sessionService.GetDeletedAsync());
    }

    [HttpPost("participants/count-from-package")]
    [Authorize(Roles = "Owner")]
    public async Task<IActionResult> CountFromPackage(CountSessionFromPackageRequest request)
    {
        return await HandleAsync(async () =>
        {
            await _sessionService.CountSessionFromPackageAsync(request);
            return NoContent();
        });
    }

    private async Task<ActionResult<T>> HandleAsync<T>(Func<Task<ActionResult<T>>> action)
    {
        try
        {
            return await action();
        }
        catch (InvalidOperationException ex)
        {
            _logger?.LogWarning("Session request {Method} {Path} rejected: {Message}",
                Request.Method, Request.Path.Value, ex.Message);
            return BadRequest(new { message = ex.Message });
        }
    }

    private async Task<IActionResult> HandleAsync(Func<Task<IActionResult>> action)
    {
        try
        {
            return await action();
        }
        catch (InvalidOperationException ex)
        {
            _logger?.LogWarning("Session request {Method} {Path} rejected: {Message}",
                Request.Method, Request.Path.Value, ex.Message);
            return BadRequest(new { message = ex.Message });
        }
    }
}
