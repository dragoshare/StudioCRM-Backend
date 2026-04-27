using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StudioCRM.Application.DTOs.TrainerRates;
using StudioCRM.Application.Interfaces;

namespace StudioCRM.Api.Controllers;

[ApiController]
[Route("api/trainer-rates")]
[Authorize]
public class TrainerRatesController : ControllerBase
{
    private readonly ITrainerRateService _trainerRateService;

    public TrainerRatesController(ITrainerRateService trainerRateService)
    {
        _trainerRateService = trainerRateService;
    }

    [HttpGet("{trainerId:int}")]
    [Authorize(Roles = "Owner,Trainer")]
    public async Task<ActionResult<List<TrainerRateDto>>> GetByTrainerId(int trainerId)
    {
        return Ok(await _trainerRateService.GetByTrainerIdAsync(trainerId));
    }

    [HttpPut("{trainerId:int}")]
    [Authorize(Roles = "Owner")]
    public async Task<ActionResult<List<TrainerRateDto>>> UpdateRates(
        int trainerId,
        UpdateTrainerRatesDto request)
    {
        var result = await _trainerRateService.UpdateRatesAsync(trainerId, request);
        return Ok(result);
    }
}