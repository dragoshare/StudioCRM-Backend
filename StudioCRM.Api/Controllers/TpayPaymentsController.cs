using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StudioCRM.Application.Interfaces;

namespace StudioCRM.Api.Controllers;

[ApiController]
[Route("api/payments/tpay")]
public class TpayPaymentsController(ITpayPaymentService payments, ILogger<TpayPaymentsController> logger) : ControllerBase
{
    [Authorize(Roles = "Client")]
    [HttpPost("packages/{clientPackageId:int}/checkout")]
    public async Task<IActionResult> Checkout(int clientPackageId, CancellationToken ct)
    {
        try { return Ok(await payments.CreateTpayCheckoutAsync(clientPackageId, ct)); }
        catch (InvalidOperationException ex) { return Conflict(new { message = ex.Message }); }
    }

    [Authorize(Roles = "Client")]
    [HttpGet("payments/{paymentId:int}")]
    public async Task<IActionResult> GetPayment(int paymentId, CancellationToken ct)
    {
        try { return Ok(await payments.GetTpayPaymentAsync(paymentId, ct)); }
        catch (InvalidOperationException ex) { return NotFound(new { message = ex.Message }); }
    }

    [AllowAnonymous]
    [HttpPost("notifications")]
    [RequestSizeLimit(65536)]
    public async Task<IActionResult> Notification(CancellationToken ct)
    {
        if (Request.ContentType?.StartsWith("application/x-www-form-urlencoded", StringComparison.OrdinalIgnoreCase) != true)
            return StatusCode(415);
        if (!Request.Headers.TryGetValue("X-JWS-Signature", out var signature) || signature.Count != 1)
            return Unauthorized();
        using var buffer = new MemoryStream();
        await Request.Body.CopyToAsync(buffer, ct);
        try
        {
            await payments.HandleTpayNotificationAsync(buffer.ToArray(), signature.ToString(), ct);
            return Content("TRUE", "text/plain");
        }
        catch (UnauthorizedAccessException) { return Unauthorized(); }
        catch (InvalidOperationException ex)
        {
            logger.LogWarning("Tpay notification requires review: {Reason}", ex.Message);
            return BadRequest("FALSE");
        }
        catch (Exception ex) when (ex is HttpRequestException || ex is OperationCanceledException && !ct.IsCancellationRequested)
        {
            return StatusCode(503, "FALSE");
        }
    }
}
