using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StudioCRM.Application.DTOs.Sessions;
using StudioCRM.Application.Interfaces;

namespace StudioCRM.Api.Controllers;

[ApiController]
[Route("api/sessions")]
[Authorize(Roles = "Owner")]
public class SessionsController : ControllerBase
{
    private readonly ISessionService _sessionService;

    public SessionsController(ISessionService sessionService)
    {
        _sessionService = sessionService;
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

    [HttpGet("filter")]
    public async Task<ActionResult<List<SessionDto>>> Filter([FromQuery] SessionFilterDto filter)
    {
        return await HandleAsync<List<SessionDto>>(async () =>
            Ok(await _sessionService.GetFilteredAsync(filter)));
    }

    [HttpPost]
    public async Task<ActionResult<SessionDto>> Create(CreateSessionDto request)
    {
        return await HandleAsync<SessionDto>(async () =>
        {
            var result = await _sessionService.CreateAsync(request);
            return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
        });
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<SessionDto>> Update(int id, UpdateSessionDto request)
    {
        return await HandleAsync<SessionDto>(async () =>
        {
            var result = await _sessionService.UpdateAsync(id, request);
            return result is null ? NotFound() : Ok(result);
        });
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var deleted = await _sessionService.DeleteAsync(id);
        if (!deleted) return NotFound();
        return NoContent();
    }
    [HttpPost("{id:int}/restore")]
    public async Task<IActionResult> Restore(int id)
    {
        return await HandleAsync(async () =>
        {
            var restored = await _sessionService.RestoreAsync(id);
            return restored ? NoContent() : NotFound();
        });
    }

    [HttpGet("deleted")]
    public async Task<ActionResult<List<SessionDto>>> GetDeleted()
    {
        return Ok(await _sessionService.GetDeletedAsync());
    }

    [HttpPost("participants/count-from-package")]
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
            return BadRequest(new { message = ex.Message });
        }
    }
}
