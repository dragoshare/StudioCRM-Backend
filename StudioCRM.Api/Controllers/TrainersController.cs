using System.Collections.Generic;
using Microsoft.AspNetCore.Mvc;
using StudioCRM.Application.DTOs.Trainers;
using StudioCRM.Application.Interfaces;

namespace StudioCRM.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class TrainersController : ControllerBase
{
    private readonly ITrainerService _trainerService;

    public TrainersController(ITrainerService trainerService)
    {
        _trainerService = trainerService;
    }

    [HttpGet]
    public async Task<ActionResult<List<TrainerDto>>> GetAll()
    {
        return Ok(await _trainerService.GetAllAsync());
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<TrainerDto>> GetById(int id)
    {
        var result = await _trainerService.GetByIdAsync(id);
        if (result is null) return NotFound();
        return Ok(result);
    }

    [HttpPost]
    public async Task<ActionResult<TrainerDto>> Create(CreateTrainerDto request)
    {
        var result = await _trainerService.CreateAsync(request);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<TrainerDto>> Update(int id, UpdateTrainerDto request)
    {
        var result = await _trainerService.UpdateAsync(id, request);
        if (result is null) return NotFound();
        return Ok(result);
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var deleted = await _trainerService.DeleteAsync(id);
        if (!deleted) return NotFound();
        return NoContent();
    }

    [HttpPost("{id:int}/restore")]
    public async Task<IActionResult> Restore(int id)
    {
        var restored = await _trainerService.RestoreAsync(id);
        if (!restored) return NotFound();
        return NoContent();
    }

    [HttpGet("deleted")]
    public async Task<ActionResult<List<TrainerDto>>> GetDeleted()
    {
        return Ok(await _trainerService.GetDeletedAsync());
    }
}