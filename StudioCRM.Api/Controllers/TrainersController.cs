using System.Collections.Generic;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StudioCRM.Application.DTOs.TrainerRates;
using StudioCRM.Application.DTOs.TrainerSettlements;
using StudioCRM.Application.DTOs.Trainers;
using StudioCRM.Application.Interfaces;

namespace StudioCRM.Api.Controllers;

[ApiController]
[Route("api/trainers")]
[Authorize(Roles = "Owner")]
public class TrainersController : ControllerBase
{
    private readonly ITrainerService _trainerService;
    private readonly ITrainerRateService _trainerRateService;
    private readonly ITrainerSettlementService _trainerSettlementService;

    public TrainersController(
        ITrainerService trainerService,
        ITrainerRateService trainerRateService,
        ITrainerSettlementService trainerSettlementService)
    {
        _trainerService = trainerService;
        _trainerRateService = trainerRateService;
        _trainerSettlementService = trainerSettlementService;
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

    [HttpPatch("{id:int}")]
    public async Task<ActionResult<TrainerDto>> Patch(int id, UpdateTrainerDto request)
    {
        return await Update(id, request);
    }

    [HttpPost("{id:int}/deactivate")]
    public async Task<IActionResult> Deactivate(int id)
    {
        return await Delete(id);
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

    [HttpGet("{id:int}/rates")]
    public async Task<ActionResult<List<TrainerRateDto>>> GetRates(int id)
    {
        return Ok(await _trainerRateService.GetByTrainerIdAsync(id));
    }

    [HttpPut("{id:int}/rates")]
    public async Task<ActionResult<List<TrainerRateDto>>> UpdateRates(int id, UpdateTrainerRatesDto request)
    {
        return Ok(await _trainerRateService.UpdateRatesAsync(id, request));
    }

    [HttpGet("{id:int}/settlement")]
    public async Task<ActionResult<TrainerMonthlySettlementDto>> GetMonthlySettlement(
        int id,
        [FromQuery] int year,
        [FromQuery] int month)
    {
        var result = await _trainerSettlementService.GetMonthlySettlementAsync(id, year, month);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpPost("{id:int}/settlement/mark-as-paid")]
    public async Task<ActionResult<TrainerMonthlySettlementDto>> MarkSettlementAsPaid(
        int id,
        [FromQuery] int year,
        [FromQuery] int month)
    {
        var result = await _trainerSettlementService.MarkAsPaidAsync(id, year, month);
        return result is null ? NotFound() : Ok(result);
    }
}
