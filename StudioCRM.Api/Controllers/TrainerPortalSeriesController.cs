using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using StudioCRM.Application.DTOs.Sessions;
using StudioCRM.Application.Interfaces;

namespace StudioCRM.Api.Controllers;

// Session series in the trainer portal: same body and responses as /api/sessions/series, scoped to the logged-in trainer.
[ApiController]
[Route("api/trainer-portal/sessions/series")]
[Authorize(Roles = "Owner,Trainer")]
public class TrainerPortalSeriesController(ITrainerPortalService trainerPortalService) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<SessionSeriesDto>> Create(CreateSessionSeriesDto request)
    {
        return await HandleAsync<SessionSeriesDto>(async () =>
            StatusCode(StatusCodes.Status201Created, await trainerPortalService.CreateSessionSeriesAsync(request)));
    }

    [HttpPost("{recurringGroupId}/sync-outlook")]
    public async Task<ActionResult<SessionSeriesOutlookSyncDto>> SyncToOutlook(
        string recurringGroupId,
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] SessionRecurrenceDto? recurrence)
    {
        return await HandleAsync<SessionSeriesOutlookSyncDto>(async () =>
        {
            var result = await trainerPortalService.SyncSessionSeriesToOutlookAsync(recurringGroupId, recurrence);
            return result is null ? NotFound() : Ok(result);
        });
    }

    [HttpDelete("{recurringGroupId}")]
    public async Task<ActionResult<DeleteSessionSeriesResultDto>> Delete(string recurringGroupId)
    {
        return await HandleAsync<DeleteSessionSeriesResultDto>(async () =>
        {
            var result = await trainerPortalService.DeleteSessionSeriesAsync(recurringGroupId);
            return result is null ? NotFound() : Ok(result);
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
}
