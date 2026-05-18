using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StudioCRM.Application.DTOs.Billing;
using StudioCRM.Application.Interfaces;

namespace StudioCRM.Api.Controllers;

[ApiController]
[Route("api/billing")]
[Authorize(Roles = "Owner")]
public class BillingController : ControllerBase
{
    private readonly IClientPaymentService _clientPaymentService;

    public BillingController(IClientPaymentService clientPaymentService)
    {
        _clientPaymentService = clientPaymentService;
    }

    [HttpGet("clients/{clientId:int}")]
    public async Task<ActionResult<ClientBillingSummaryDto>> GetClientBilling(int clientId)
    {
        return await HandleAsync<ClientBillingSummaryDto>(async () =>
            Ok(await _clientPaymentService.GetClientSummaryAsync(clientId)));
    }

    [HttpGet("payments/pending")]
    public async Task<ActionResult<List<ClientPaymentDto>>> GetPendingPayments()
    {
        return await HandleAsync<List<ClientPaymentDto>>(async () =>
            Ok(await _clientPaymentService.GetPendingConfirmationsAsync()));
    }

    [HttpGet("payments")]
    public async Task<ActionResult<PagedResultDto<ClientPaymentDto>>> GetPayments(
        [FromQuery] ClientPaymentFilterDto filter)
    {
        return await HandleAsync<PagedResultDto<ClientPaymentDto>>(async () =>
            Ok(await _clientPaymentService.GetPaymentsAsync(filter)));
    }

    [HttpGet("clients/{clientId:int}/payments")]
    public async Task<ActionResult<PagedResultDto<ClientPaymentDto>>> GetClientPayments(
        int clientId,
        [FromQuery] ClientPaymentFilterDto filter)
    {
        return await HandleAsync<PagedResultDto<ClientPaymentDto>>(async () =>
            Ok(await _clientPaymentService.GetClientPaymentsAsync(clientId, filter)));
    }

    [HttpGet("clients/{clientId:int}/active-package")]
    public async Task<ActionResult<ClientPackageBillingDto>> GetActivePackage(int clientId)
    {
        return await HandleAsync<ClientPackageBillingDto>(async () =>
        {
            var result = await _clientPaymentService.GetActivePackageAsync(clientId);
            return result is null ? NotFound() : Ok(result);
        });
    }

    [HttpPost("payments")]
    public async Task<ActionResult<ClientPaymentDto>> CreateStaffPayment(CreateClientPaymentRequest request)
    {
        return await HandleAsync<ClientPaymentDto>(async () =>
        {
            var payment = await _clientPaymentService.CreatePaymentAsStaffAsync(request);
            return CreatedAtAction(nameof(GetClientBilling), new { clientId = payment.ClientId }, payment);
        });
    }

    [HttpPost("payments/{paymentId:int}/confirm")]
    public async Task<ActionResult<ClientPaymentDto>> ConfirmPayment(int paymentId)
    {
        return await HandleAsync<ClientPaymentDto>(async () =>
            Ok(await _clientPaymentService.ConfirmAsync(paymentId)));
    }

    [HttpPost("payments/{paymentId:int}/reject")]
    public async Task<ActionResult<ClientPaymentDto>> RejectPayment(
        int paymentId,
        RejectClientPaymentRequest request)
    {
        return await HandleAsync<ClientPaymentDto>(async () =>
            Ok(await _clientPaymentService.RejectAsync(paymentId, request)));
    }

    [HttpPost("payments/{paymentId:int}/receipt/issue")]
    public async Task<ActionResult<ClientPaymentDto>> IssueReceipt(
        int paymentId,
        [FromBody] IssueReceiptRequest? request)
    {
        return await HandleAsync<ClientPaymentDto>(async () =>
            Ok(await _clientPaymentService.IssueReceiptAsync(
                paymentId,
                request ?? new IssueReceiptRequest())));
    }

    [HttpPost("payments/{paymentId:int}/receipt/cancel")]
    public async Task<ActionResult<ClientPaymentDto>> CancelReceipt(int paymentId)
    {
        return await HandleAsync<ClientPaymentDto>(async () =>
            Ok(await _clientPaymentService.CancelReceiptAsync(paymentId)));
    }

    [HttpPost("payments/{paymentId:int}/reverse")]
    public async Task<ActionResult<ClientPaymentDto>> ReversePayment(
        int paymentId,
        [FromBody] ReverseClientPaymentRequest? request)
    {
        return await HandleAsync<ClientPaymentDto>(async () =>
            Ok(await _clientPaymentService.ReverseAsync(
                paymentId,
                request ?? new ReverseClientPaymentRequest())));
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
