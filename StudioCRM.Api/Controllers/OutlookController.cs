using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using StudioCRM.Infrastructure.Persistence;
using StudioCRM.Application.DTOs.Calendar;
using StudioCRM.Application.Interfaces.Calendar;

namespace StudioCRM.Api.Controllers;

[ApiController]
[Route("api/outlook")]
public class OutlookController : ControllerBase
{
    private readonly IOutlookCalendarAuthService _authService;
    private readonly IOutlookCalendarSyncService _syncService;
    private readonly IOutlookSubscriptionService _subscriptionService;
    private readonly IOutlookWebhookService _webhookService;
    private readonly IExternalCalendarEventService _externalEventService;
    private readonly IOutlookContactService _outlookContactService;

    public OutlookController(
        IOutlookCalendarAuthService authService,
        IOutlookCalendarSyncService syncService,
        IOutlookSubscriptionService subscriptionService,
        IOutlookWebhookService webhookService,
        IExternalCalendarEventService externalEventService,
        IOutlookContactService outlookContactService)
    {
        _authService = authService;
        _syncService = syncService;
        _subscriptionService = subscriptionService;
        _webhookService = webhookService;
        _externalEventService = externalEventService;
        _outlookContactService = outlookContactService;
    }

    private Task<bool> CanManageSession(StudioCRMDbContext db, int id)
    {
        if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId)) return Task.FromResult(false);
        return db.Sessions.AnyAsync(s => s.Id == id && (User.IsInRole("Owner") || s.Trainer.UserId == userId));
    }

    [HttpGet("connect-url")]
    [Authorize(Roles = "Trainer,Owner")]
    public async Task<ActionResult<CalendarConnectUrlDto>> GetConnectUrl()
    {
        try
        {
            return Ok(await _authService.GetConnectUrlAsync());
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpGet("callback")]
    [AllowAnonymous]
    public async Task<IActionResult> Callback(
        [FromQuery] string? code,
        [FromQuery] string? state,
        [FromQuery] string? error = null,
        [FromQuery(Name = "error_description")] string? errorDescription = null)
    {
        // Microsoft returns error/error_description instead of code when sign-in or consent fails.
        // The pending challenge simply expires; nothing is connected.
        if (!string.IsNullOrWhiteSpace(error))
        {
            var failure = StudioCRM.Infrastructure.Services.Calendar.OutlookOAuthError.Describe(error, errorDescription);
            HttpContext.RequestServices?.GetService<ILogger<OutlookController>>()?.LogWarning(
                "Outlook OAuth callback returned Microsoft error {Error} ({MicrosoftCode}): {Detail}",
                failure.MicrosoftError, failure.MicrosoftCode, failure.Detail);
            return BadRequest(new
            {
                message = failure.Message,
                code = failure.Code,
                microsoftError = failure.MicrosoftError,
                microsoftCode = failure.MicrosoftCode,
                detail = failure.Detail
            });
        }

        try
        {
            await _authService.ConnectCallbackAsync(code ?? string.Empty, state);

            return Ok(new
            {
                message = "Outlook calendar connected successfully. You can close this tab and return to StudioCRM."
            });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpGet("status")]
    [Authorize(Roles = "Trainer,Owner")]
    public async Task<ActionResult<CalendarIntegrationStatusDto>> Status()
    {
        return Ok(await _authService.GetStatusAsync());
    }

    [HttpPost("disconnect")]
    [Authorize(Roles = "Trainer,Owner")]
    public async Task<IActionResult> Disconnect()
    {
        await _authService.DisconnectAsync();
        return NoContent();
    }

    [HttpPost("sync-session/{sessionId:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [Authorize(Roles = "Trainer,Owner")]
    public async Task<IActionResult> SyncSession(int sessionId, [FromServices] StudioCRMDbContext db)
    {
        try
        {
            if (!await CanManageSession(db, sessionId)) return NotFound();
            await _syncService.SyncSessionAsync(sessionId);
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpDelete("sync-session/{sessionId:int}")]
    [Authorize(Roles = "Trainer,Owner")]
    public async Task<IActionResult> DeleteSessionEvent(int sessionId, [FromServices] StudioCRMDbContext db)
    {
        try
        {
            if (!await CanManageSession(db, sessionId)) return NotFound();
            await _syncService.DeleteSessionEventAsync(sessionId);
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("subscription/create")]
    [Authorize(Roles = "Trainer,Owner")]
    public async Task<IActionResult> CreateSubscription()
    {
        try
        {
            await _subscriptionService.CreateSubscriptionAsync();
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("subscription/renew")]
    [Authorize(Roles = "Owner")]
    public async Task<IActionResult> RenewSubscriptions()
    {
        await _subscriptionService.RenewExpiringSubscriptionsAsync();
        return NoContent();
    }

    [HttpPost("webhook")]
    [AllowAnonymous]
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> Webhook(
        [FromServices] StudioCRM.Api.BackgroundServices.OutlookWebhookQueue queue,
        [FromServices] IConfiguration configuration)
    {
        if (Request.Query.TryGetValue("validationToken", out var token))
            return Content(token.ToString(), "text/plain");

        // Accepted (not 503) so Graph stops retrying the dropped notifications.
        if (configuration.GetValue<bool>("Outlook:EmergencyStop"))
            return Accepted();

        // Graph notifications are small; anything larger is not a genuine notification batch.
        if (Request.ContentLength > 1_000_000)
            return StatusCode(StatusCodes.Status413PayloadTooLarge);

        using var reader = new StreamReader(Request.Body);
        var body = await reader.ReadToEndAsync();

        if (string.IsNullOrWhiteSpace(body))
            return Ok();

        // Answer immediately; the subscription and clientState are verified when the queued body is processed.
        // A full queue returns 503 so Graph retries the delivery later.
        return queue.TryEnqueue(body) ? Accepted() : StatusCode(StatusCodes.Status503ServiceUnavailable);
    }

    [HttpPost("reconcile")]
    [Authorize(Roles = "Trainer,Owner")]
    [ProducesResponseType(typeof(OutlookReconciliationResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(OutlookReconcileJobStatusDto), StatusCodes.Status202Accepted)]
    public async Task<IActionResult> Reconcile(
        [FromBody(EmptyBodyBehavior = Microsoft.AspNetCore.Mvc.ModelBinding.EmptyBodyBehavior.Allow)]
        OutlookReconciliationRequestDto? request,
        [FromServices] StudioCRM.Api.BackgroundServices.OutlookReconcileJobs jobs,
        [FromServices] StudioCRM.Api.Security.RequestOrganizationContext organization)
    {
        if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId)) return Unauthorized();
        var actor = new OutlookReconcileActor(userId, User.IsInRole("Owner"), User.IsInRole("Trainer"));
        request ??= new OutlookReconciliationRequestDto();
        if (request.TrainerId.HasValue && !actor.IsOwner) return Forbid();

        // Runs in the background so a long reconcile is not cut off by the browser or proxy. Short ones still
        // answer with the result as before; longer ones answer 202 and finish in the background.
        var job = jobs.Enqueue(organization.OrganizationId, actor, request);
        await Task.WhenAny(job.Done, Task.Delay(ReconcileWait, HttpContext.RequestAborted));
        if (job.State == StudioCRM.Api.BackgroundServices.OutlookReconcileJobState.Completed) return Ok(job.Result);
        if (job.State == StudioCRM.Api.BackgroundServices.OutlookReconcileJobState.Failed)
            return job.IsUserError ? BadRequest(new { message = job.Error }) : StatusCode(StatusCodes.Status500InternalServerError, new { message = job.Error });
        return Accepted($"/api/outlook/reconcile/{job.Id}", ReconcileStatus(job));
    }

    private static readonly TimeSpan ReconcileWait = TimeSpan.FromSeconds(15);

    [HttpGet("reconcile/{jobId:guid}")]
    [Authorize(Roles = "Trainer,Owner")]
    [ProducesResponseType(typeof(OutlookReconcileJobStatusDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public IActionResult ReconcileJob(
        Guid jobId,
        [FromServices] StudioCRM.Api.BackgroundServices.OutlookReconcileJobs jobs,
        [FromServices] StudioCRM.Api.Security.RequestOrganizationContext organization)
    {
        var job = jobs.Find(jobId);
        if (job is null || job.OrganizationId != organization.OrganizationId) return NotFound();
        if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId)) return Unauthorized();
        if (job.Actor.UserId != userId && !User.IsInRole("Owner")) return NotFound();
        return Ok(ReconcileStatus(job));
    }

    private static OutlookReconcileJobStatusDto ReconcileStatus(StudioCRM.Api.BackgroundServices.OutlookReconcileJob job) => new()
    {
        JobId = job.Id,
        State = job.State.ToString(),
        Message = job.State switch
        {
            StudioCRM.Api.BackgroundServices.OutlookReconcileJobState.Completed => "Synchronizacja z Outlookiem zakończona.",
            StudioCRM.Api.BackgroundServices.OutlookReconcileJobState.Failed => job.Error,
            _ => "Synchronizacja z Outlookiem trwa w tle. Grafik odświeży się po jej zakończeniu."
        },
        Result = job.Result,
        QueuedAt = job.QueuedAt,
        StartedAt = job.StartedAt,
        FinishedAt = job.FinishedAt
    };

    [HttpGet("imported-events")]
    [Authorize(Roles = "Trainer,Owner")]
    public async Task<IActionResult> GetImportedEvents()
    {
        return Ok(await _externalEventService.GetImportedEventsAsync());
    }

    [HttpGet("issues")]
    [ProducesResponseType(typeof(List<OutlookMappingIssueDto>), StatusCodes.Status200OK)]
    [Authorize(Roles = "Trainer,Owner")]
    public async Task<IActionResult> GetIssues()
    {
        return Ok(await _externalEventService.GetIssuesAsync());
    }

    [HttpPost("imported-events/{id:int}/convert-to-session")]
    [Authorize(Roles = "Trainer,Owner")]
    public async Task<IActionResult> ConvertImportedEvent(
        int id,
        [FromBody] ConvertExternalEventToSessionDto request)
    {
        try
        {
            var sessionId = await _externalEventService.ConvertToSessionAsync(id, request);

            return Ok(new
            {
                sessionId,
                message = "Imported Outlook event converted to CRM session."
            });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("issues/send-invite")]
    [Authorize(Roles = "Trainer,Owner")]
    public async Task<IActionResult> SendInvite([FromBody] SendInviteFromOutlookIssueDto request)
    {
        try
        {
            await _externalEventService.SendInviteFromIssueAsync(request.Email);
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }
    [HttpPost("issues/link-client")]
    [Authorize(Roles = "Trainer,Owner")]
    public async Task<IActionResult> LinkClient([FromBody] LinkClientFromIssueDto request)
    {
        try
        {
            await _externalEventService.LinkClientFromIssueAsync(
                request.ClientId,
                request.Email);

            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }
    [HttpPost("issues/ignore")]
    [Authorize(Roles = "Trainer,Owner")]
    public async Task<IActionResult> IgnoreIssue([FromBody] IgnoreOutlookIssueDto request)
    {
        try
        {
            await _externalEventService.IgnoreIssueAsync(
                request.ExternalCalendarEventId,
                request.Message);

            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }
    [HttpPost("contacts/prepare-calendar-addresses")]
    [Authorize(Roles = "Owner")]
    public async Task<IActionResult> PrepareCalendarAddresses()
    {
        try
        {
            return Ok(new { prepared = await _outlookContactService.PrepareCalendarAddressesAsync() });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("contacts/sync-clients")]
    [Authorize(Roles = "Trainer,Owner")]
    public async Task<IActionResult> SyncClientsToOutlookContacts()
    {
        try
        {
            await _outlookContactService.SyncClientsAsync();
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }
    [AllowAnonymous]
    [HttpGet("subscription/keepalive")]
    [HttpHead("subscription/keepalive")]
    public async Task<IActionResult> KeepAlive([FromServices] IConfiguration configuration)
    {
        if (!StudioCRM.Api.Security.WorkerRequestAccess.CanExecute(HttpContext, configuration))
            return Ok(new { message = "Service awake; subscriptions are handled by the background worker.", executed = false, utcNow = DateTime.UtcNow });
        await _subscriptionService.RenewExpiringSubscriptionsAsync();

        return Ok(new
        {
            message = "Outlook subscriptions checked",
            utcNow = DateTime.UtcNow
        });
    }
}
