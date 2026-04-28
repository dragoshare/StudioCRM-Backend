using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StudioCRM.Application.DTOs.Sessions;
using StudioCRM.Application.Interfaces;

namespace StudioCRM.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class SessionsController : ControllerBase
{
    private readonly ISessionService _sessionService;

    public SessionsController(ISessionService sessionService)
    {
        _sessionService = sessionService;
    }

    [HttpGet]
    [Authorize(Roles = "Owner,Trainer")]
    public async Task<ActionResult<List<SessionDto>>> GetAll()
    {
        return Ok(await _sessionService.GetAllAsync());
    }

    [HttpGet("{id:int}")]
    [Authorize(Roles = "Owner,Trainer")]
    public async Task<ActionResult<SessionDto>> GetById(int id)
    {
        var result = await _sessionService.GetByIdAsync(id);
        if (result is null) return NotFound();
        return Ok(result);
    }

    [HttpGet("filter")]
    [Authorize(Roles = "Owner,Trainer")]
    public async Task<ActionResult<List<SessionDto>>> Filter([FromQuery] SessionFilterDto filter)
    {
        return Ok(await _sessionService.GetFilteredAsync(filter));
    }

    [HttpPost]
    [Authorize(Roles = "Owner,Trainer")]
    public async Task<ActionResult<SessionDto>> Create(CreateSessionDto request)
    {
        var result = await _sessionService.CreateAsync(request);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    [HttpPut("{id:int}")]
    [Authorize(Roles = "Owner,Trainer")]
    public async Task<ActionResult<SessionDto>> Update(int id, UpdateSessionDto request)
    {
        var result = await _sessionService.UpdateAsync(id, request);
        if (result is null) return NotFound();
        return Ok(result);
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
        var restored = await _sessionService.RestoreAsync(id);
        if (!restored) return NotFound();
        return NoContent();
    }

    [HttpGet("deleted")]
    [Authorize(Roles = "Owner")]
    public async Task<ActionResult<List<SessionDto>>> GetDeleted()
    {
        return Ok(await _sessionService.GetDeletedAsync());
    }

    [Authorize(Roles = "Owner,Trainer")]
    [HttpPost("participants/count-from-package")]
    public async Task<IActionResult> CountFromPackage(CountSessionFromPackageRequest request)
    {
        await _sessionService.CountSessionFromPackageAsync(request);
        return NoContent();
    }
}