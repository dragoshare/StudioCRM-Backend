using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StudioCRM.Application.DTOs.TrainerSettlements;
using StudioCRM.Application.Interfaces;

namespace StudioCRM.Api.Controllers;

[ApiController]
[Route("api/trainer-settlements")]
[Authorize]
public class TrainerSettlementsController : ControllerBase
{
    private readonly ITrainerSettlementService _trainerSettlementService;

    public TrainerSettlementsController(ITrainerSettlementService trainerSettlementService)
    {
        _trainerSettlementService = trainerSettlementService;
    }

    [HttpGet("{trainerId:int}")]
    [Authorize(Roles = "Owner,Trainer")]
    public async Task<ActionResult<TrainerMonthlySettlementDto>> GetMonthlySettlement(
        int trainerId,
        [FromQuery] int year,
        [FromQuery] int month)
    {
        var result = await _trainerSettlementService.GetMonthlySettlementAsync(trainerId, year, month);

        if (result is null)
            return NotFound();

        return Ok(result);
    }

    [HttpPost("{trainerId:int}/mark-as-paid")]
    [Authorize(Roles = "Owner")]
    public async Task<ActionResult<TrainerMonthlySettlementDto>> MarkAsPaid(
        int trainerId,
        [FromQuery] int year,
        [FromQuery] int month)
    {
        var result = await _trainerSettlementService.MarkAsPaidAsync(trainerId, year, month);

        if (result is null)
            return NotFound();

        return Ok(result);
    }
}