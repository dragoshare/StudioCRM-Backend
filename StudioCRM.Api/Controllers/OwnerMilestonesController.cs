using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StudioCRM.Application.DTOs.Milestones;
using StudioCRM.Application.Interfaces;

namespace StudioCRM.Api.Controllers;

[ApiController]
[Route("api/owner")]
[Authorize(Roles = "Owner")]
public class OwnerMilestonesController : ControllerBase
{
    private readonly IMilestoneService _milestoneService;

    public OwnerMilestonesController(IMilestoneService milestoneService)
    {
        _milestoneService = milestoneService;
    }

    [HttpGet("milestones/rewards-pending")]
    public async Task<IActionResult> GetPendingRewards()
    {
        var rewards = await _milestoneService.GetPendingRewardsForOwnerAsync();

        return Ok(rewards);
    }

    [HttpGet("milestones/definitions")]
    public async Task<ActionResult<List<MilestoneDefinitionDto>>> GetDefinitions(
        [FromQuery] bool includeInactive = false)
    {
        return Ok(await _milestoneService.GetDefinitionsAsync(includeInactive));
    }

    [HttpPost("milestones/definitions")]
    public async Task<ActionResult<MilestoneDefinitionDto>> CreateDefinition(
        [FromBody] UpsertMilestoneDefinitionRequest request)
    {
        return await HandleAsync<MilestoneDefinitionDto>(async () =>
        {
            var definition = await _milestoneService.CreateDefinitionAsync(request);
            return CreatedAtAction(
                nameof(GetDefinitions),
                new { includeInactive = true },
                definition);
        });
    }

    [HttpPut("milestones/definitions/{id:int}")]
    public async Task<ActionResult<MilestoneDefinitionDto>> UpdateDefinition(
        int id,
        [FromBody] UpsertMilestoneDefinitionRequest request)
    {
        return await HandleAsync<MilestoneDefinitionDto>(async () =>
        {
            var definition = await _milestoneService.UpdateDefinitionAsync(id, request);
            return definition is null ? NotFound() : Ok(definition);
        });
    }

    [HttpPatch("milestones/definitions/{id:int}/deactivate")]
    public async Task<ActionResult<MilestoneDefinitionDto>> DeactivateDefinition(int id)
    {
        return await HandleAsync<MilestoneDefinitionDto>(async () =>
        {
            var definition = await _milestoneService.SetDefinitionActiveAsync(id, false);
            return definition is null ? NotFound() : Ok(definition);
        });
    }

    [HttpPatch("milestones/definitions/{id:int}/restore")]
    public async Task<ActionResult<MilestoneDefinitionDto>> RestoreDefinition(int id)
    {
        return await HandleAsync<MilestoneDefinitionDto>(async () =>
        {
            var definition = await _milestoneService.SetDefinitionActiveAsync(id, true);
            return definition is null ? NotFound() : Ok(definition);
        });
    }

    [HttpGet("clients/{clientId:int}/milestones")]
    public async Task<IActionResult> GetClientMilestones(int clientId)
    {
        var milestones = await _milestoneService.GetClientMilestonesAsync(clientId);

        if (milestones is null)
            return NotFound();

        return Ok(milestones);
    }

    [HttpPatch("clients/{clientId:int}/milestones/{milestoneDefinitionId:int}/claim")]
    public async Task<IActionResult> ClaimReward(
        int clientId,
        int milestoneDefinitionId,
        [FromBody] ClaimMilestoneRewardRequest request)
    {
        var userId = GetCurrentUserId();

        if (userId is null)
            return Unauthorized();

        var success = await _milestoneService.ClaimRewardAsOwnerAsync(
            userId.Value,
            clientId,
            milestoneDefinitionId,
            request.Note);

        if (!success)
        {
            return BadRequest(new
            {
                message = "Nie udało się oznaczyć nagrody jako wydanej. Sprawdź, czy milestone jest osiągnięty."
            });
        }

        return Ok(new
        {
            message = "Nagroda została oznaczona jako wydana."
        });
    }

    [HttpPatch("clients/{clientId:int}/milestones/{milestoneDefinitionId:int}/unclaim")]
    public async Task<IActionResult> UnclaimReward(
        int clientId,
        int milestoneDefinitionId)
    {
        var userId = GetCurrentUserId();

        if (userId is null)
            return Unauthorized();

        var success = await _milestoneService.UnclaimRewardAsOwnerAsync(
            userId.Value,
            clientId,
            milestoneDefinitionId);

        if (!success)
        {
            return BadRequest(new
            {
                message = "Nie udało się cofnąć wydania nagrody. Sprawdź, czy nagroda była oznaczona jako wydana."
            });
        }

        return Ok(new
        {
            message = "Wydanie nagrody zostało cofnięte."
        });
    }

    private int? GetCurrentUserId()
    {
        var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (!int.TryParse(userIdClaim, out var userId))
            return null;

        return userId;
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
}
