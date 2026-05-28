using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StudioCRM.Application.DTOs.Locations;
using StudioCRM.Application.Interfaces;

namespace StudioCRM.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class LocationsController : ControllerBase
{
    private readonly ILocationService _locationService;

    public LocationsController(ILocationService locationService)
    {
        _locationService = locationService;
    }

    [HttpGet]
    [Authorize(Roles = "Owner")]
    public async Task<ActionResult<List<LocationDto>>> GetAll()
    {
        return Ok(await _locationService.GetAllAsync());
    }

    [HttpGet("{id:int}")]
    [Authorize(Roles = "Owner,Trainer")]
    public async Task<ActionResult<LocationDto>> GetById(int id)
    {
        var result = await _locationService.GetByIdAsync(id);
        if (result is null) return NotFound();
        return Ok(result);
    }

    [HttpPost]
    [Authorize(Roles = "Owner")]
    public async Task<ActionResult<LocationDto>> Create(CreateLocationDto request)
    {
        var result = await _locationService.CreateAsync(request);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    [HttpPut("{id:int}")]
    [Authorize(Roles = "Owner")]
    public async Task<ActionResult<LocationDto>> Update(int id, UpdateLocationDto request)
    {
        var result = await _locationService.UpdateAsync(id, request);
        if (result is null) return NotFound();
        return Ok(result);
    }
}
