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

    [HttpPost("subscription/renew")]
    [Authorize(Roles = "Trainer,Owner")]
    public async Task<IActionResult> RenewSubscriptions()
    {
        await _subscriptionService.RenewExpiringSubscriptionsAsync();
        return NoContent();
    }

    [HttpPost("webhook")]
    [AllowAnonymous]
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> Webhook()
    {
        if (Request.Query.TryGetValue("validationToken", out var token))
            return Content(token.ToString(), "text/plain");

        using var reader = new StreamReader(Request.Body);
        var body = await reader.ReadToEndAsync();

        await _webhookService.HandleNotificationAsync(body);

        return Ok();
    }

    [HttpGet("imported-events")]
    [Authorize(Roles = "Trainer,Owner")]
    public async Task<IActionResult> GetImportedEvents()
    {
        return Ok(await _externalEventService.GetImportedEventsAsync());
    }

    [HttpGet("issues")]
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
    public async Task<IActionResult> KeepAlive()
    {
        await _subscriptionService.RenewExpiringSubscriptionsAsync();

        return Ok(new
        {
            message = "Outlook subscriptions checked",
            utcNow = DateTime.UtcNow
        });
    }
}
