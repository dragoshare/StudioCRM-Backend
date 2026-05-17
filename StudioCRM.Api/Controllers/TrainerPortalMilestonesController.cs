using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StudioCRM.Application.DTOs.Milestones;
using StudioCRM.Application.Interfaces;

namespace StudioCRM.Api.Controllers;

[ApiController]
[Route("api/trainer-portal")]
[Authorize(Roles = "Owner,Trainer")]
public class TrainerPortalMilestonesController : ControllerBase
{
    private readonly IMilestoneService _milestoneService;

    public TrainerPortalMilestonesController(IMilestoneService milestoneService)
    {
        _milestoneService = milestoneService;
    }

    [HttpGet("milestones/rewards-pending")]
    public async Task<IActionResult> GetPendingRewards()
    {
        var userId = GetCurrentUserId();

        if (userId is null)
            return Unauthorized();

        var rewards = await _milestoneService.GetPendingRewardsForTrainerAsync(userId.Value);

        return Ok(rewards);
    }

    [HttpGet("clients/{clientId:int}/milestones")]
    public async Task<IActionResult> GetClientMilestones(int clientId)
    {
        var userId = GetCurrentUserId();

        if (userId is null)
            return Unauthorized();

        var hasAccess = await _milestoneService.TrainerHasAccessToClientAsync(
            userId.Value,
            clientId);

        if (!hasAccess)
            return Forbid();

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

        var success = await _milestoneService.ClaimRewardAsTrainerAsync(
            userId.Value,
            clientId,
            milestoneDefinitionId,
            request.Note);

        if (!success)
        {
            return BadRequest(new
            {
                message = "Nie udało się oznaczyć nagrody jako wydanej. Sprawdź, czy klient należy do trenera i czy milestone jest osiągnięty."
            });
        }

        return Ok(new
        {
            message = "Nagroda została oznaczona jako wydana."
        });
    }

    private int? GetCurrentUserId()
    {
        var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (!int.TryParse(userIdClaim, out var userId))
            return null;

        return userId;
    }
}
