using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StudioCRM.Application.DTOs.Billing;
using StudioCRM.Application.Interfaces;

namespace StudioCRM.Api.Controllers;

[ApiController]
[Route("api/billing/payment-configuration")]
[Authorize(Roles = "Owner")]
public class PaymentConfigurationController : ControllerBase
{
    private readonly IPaymentConfigurationService _paymentConfigurationService;

    public PaymentConfigurationController(IPaymentConfigurationService paymentConfigurationService)
    {
        _paymentConfigurationService = paymentConfigurationService;
    }

    [HttpGet]
    public async Task<ActionResult<PaymentConfigurationDto>> GetConfiguration()
    {
        return await HandleAsync<PaymentConfigurationDto>(async () =>
            Ok(await _paymentConfigurationService.GetConfigurationAsync()));
    }

    [HttpPost("tpay/{accountKey}/test-connection")]
    public async Task<IActionResult> TestTpayConnection(
        string accountKey,
        [FromServices] ITpayConnectionService tpay,
        CancellationToken cancellationToken)
    {
        try
        {
            var sandbox = await tpay.TestConnectionAsync(accountKey, cancellationToken);
            return Ok(new { connected = true, accountKey, sandbox });
        }
        catch (InvalidOperationException)
        {
            return BadRequest(new { message = "Tpay credentials are not configured for this account key." });
        }
        catch (Exception ex) when (ex is HttpRequestException or System.Text.Json.JsonException ||
                                   ex is OperationCanceledException && !cancellationToken.IsCancellationRequested)
        {
            return StatusCode(502, new { message = "Tpay connection check failed. Check credentials and environment or retry later." });
        }
    }

    [HttpPost("legal-entities")]
    public async Task<ActionResult<LegalEntityDto>> CreateLegalEntity(UpsertLegalEntityRequest request)
    {
        return await HandleAsync<LegalEntityDto>(async () =>
        {
            var result = await _paymentConfigurationService.CreateLegalEntityAsync(request);
            return CreatedAtAction(nameof(GetConfiguration), new { id = result.Id }, result);
        });
    }

    [HttpPut("legal-entities/{id:int}")]
    public async Task<ActionResult<LegalEntityDto>> UpdateLegalEntity(
        int id,
        UpsertLegalEntityRequest request)
    {
        return await HandleAsync<LegalEntityDto>(async () =>
        {
            var result = await _paymentConfigurationService.UpdateLegalEntityAsync(id, request);
            return result is null ? NotFound() : Ok(result);
        });
    }

    [HttpPost("provider-accounts")]
    public async Task<ActionResult<PaymentProviderAccountDto>> CreatePaymentProviderAccount(
        UpsertPaymentProviderAccountRequest request)
    {
        return await HandleAsync<PaymentProviderAccountDto>(async () =>
        {
            var result = await _paymentConfigurationService.CreatePaymentProviderAccountAsync(request);
            return CreatedAtAction(nameof(GetConfiguration), new { id = result.Id }, result);
        });
    }

    [HttpPut("provider-accounts/{id:int}")]
    public async Task<ActionResult<PaymentProviderAccountDto>> UpdatePaymentProviderAccount(
        int id,
        UpsertPaymentProviderAccountRequest request)
    {
        return await HandleAsync<PaymentProviderAccountDto>(async () =>
        {
            var result = await _paymentConfigurationService.UpdatePaymentProviderAccountAsync(id, request);
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
            return BadRequest(new
            {
                message = ex.Message
            });
        }
    }
}
