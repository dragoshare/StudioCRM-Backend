using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StudioCRM.Application.DTOs.Clients;
using StudioCRM.Application.Interfaces;

namespace StudioCRM.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ClientsController : ControllerBase
{
    private readonly IClientService _clientService;

    public ClientsController(IClientService clientService)
    {
        _clientService = clientService;
    }

    [HttpGet]
    [Authorize(Roles = "Owner,Trainer")]
    public async Task<ActionResult<List<ClientDto>>> GetAll()
    {
        return Ok(await _clientService.GetAllAsync());
    }

    [HttpGet("{id:int}")]
    [Authorize(Roles = "Owner,Trainer")]
    public async Task<ActionResult<ClientDto>> GetById(int id)
    {
        var result = await _clientService.GetByIdAsync(id);
        if (result is null) return NotFound();
        return Ok(result);
    }

    [HttpGet("filter")]
    [Authorize(Roles = "Owner,Trainer")]
    public async Task<ActionResult<List<ClientDto>>> Filter([FromQuery] ClientFilterDto filter)
    {
        return Ok(await _clientService.GetFilteredAsync(filter));
    }

    [HttpPost]
    [Authorize(Roles = "Owner,Trainer")]
    public async Task<ActionResult<ClientDto>> Create(CreateClientDto request)
    {
        var result = await _clientService.CreateAsync(request);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    [HttpPut("{id:int}")]
    [Authorize(Roles = "Owner,Trainer")]
    public async Task<ActionResult<ClientDto>> Update(int id, UpdateClientDto request)
    {
        var result = await _clientService.UpdateAsync(id, request);
        if (result is null) return NotFound();
        return Ok(result);
    }

    [HttpDelete("{id:int}")]
    [Authorize(Roles = "Owner")]
    public async Task<IActionResult> Delete(int id)
    {
        try
        {
            var deleted = await _clientService.DeleteAsync(id);
            if (!deleted) return NotFound();
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }
    [HttpPost("{id}/restore")]
    [Authorize(Roles = "Owner")]
    public async Task<IActionResult> Restore(int id)
    {
        var result = await _clientService.RestoreAsync(id);

        if (!result)
        {
            return NotFound();
        }

        return NoContent();
    }

    [HttpGet("deleted")]
    [Authorize(Roles = "Owner")]
    public async Task<ActionResult<List<ClientDto>>> GetDeleted()
    {
        return Ok(await _clientService.GetDeletedAsync());
    }
}