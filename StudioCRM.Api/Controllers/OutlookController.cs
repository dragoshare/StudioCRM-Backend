using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
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

    public OutlookController(
        IOutlookCalendarAuthService authService,
        IOutlookCalendarSyncService syncService,
        IOutlookSubscriptionService subscriptionService,
        IOutlookWebhookService webhookService,
        IExternalCalendarEventService externalEventService)
    {
        _authService = authService;
        _syncService = syncService;
        _subscriptionService = subscriptionService;
        _webhookService = webhookService;
        _externalEventService = externalEventService;
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
    public async Task<IActionResult> Callback([FromQuery] string code, [FromQuery] string? state)
    {
        try
        {
            await _authService.ConnectCallbackAsync(code, state);

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
    [Authorize(Roles = "Trainer,Owner")]
    public async Task<IActionResult> SyncSession(int sessionId)
    {
        try
        {
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
    public async Task<IActionResult> DeleteSessionEvent(int sessionId)
    {
        try
        {
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

    [HttpPost("webhook")]
    [AllowAnonymous]
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> Webhook()
    {
        if (Request.Query.TryGetValue("validationToken", out var token))
        {
            return Content(token.ToString(), "text/plain");
        }

        using var reader = new StreamReader(Request.Body);
        var body = await reader.ReadToEndAsync();

        await _webhookService.HandleNotificationAsync(body);

        return Ok();
    }

    [HttpGet("imported-events")]
    [Authorize(Roles = "Trainer,Owner")]
    public async Task<IActionResult> GetImportedEvents()
    {
        var events = await _externalEventService.GetImportedEventsAsync();
        return Ok(events);
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
}