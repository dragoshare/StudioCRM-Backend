using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StudioCRM.Application.DTOs.Billing;
using StudioCRM.Application.DTOs.ClientPortal;
using StudioCRM.Application.DTOs.Profiles;
using StudioCRM.Application.DTOs.Subscriptions;
using StudioCRM.Application.DTOs.TrainingPlans;
using StudioCRM.Application.Interfaces;
using StudioCRM.Infrastructure.Persistence;

namespace StudioCRM.Api.Controllers;

[ApiController]
[Route("api/client-portal")]
[Authorize(Roles = "Client")]
public class ClientPortalController : ControllerBase
{
    private readonly IClientPortalService _clientPortalService;
    private readonly IClientPaymentService _clientPaymentService;
    private readonly IMilestoneService _milestoneService;
    private readonly ISubscriptionService _subscriptionService;
    private readonly StudioCRMDbContext _context;

    public ClientPortalController(
    IClientPortalService clientPortalService,
    IClientPaymentService clientPaymentService,
    IMilestoneService milestoneService,
    ISubscriptionService subscriptionService,
    StudioCRMDbContext context)
    {
        _clientPortalService = clientPortalService;
        _clientPaymentService = clientPaymentService;
        _milestoneService = milestoneService;
        _subscriptionService = subscriptionService;
        _context = context;
    }

    [HttpGet("me")]
    public async Task<ActionResult<ClientPortalMeDto>> GetMe()
    {
        var result = await _clientPortalService.GetMeAsync();

        if (result is null)
            return NotFound();

        return Ok(result);
    }

    [HttpPatch("me")]
    public async Task<ActionResult<ClientPortalMeDto>> UpdateMe(UpdateClientPortalProfileRequest request)
    {
        return await HandleAsync<ClientPortalMeDto>(async () =>
        {
            var result = await _clientPortalService.UpdateMeAsync(request);
            return result is null ? NotFound() : Ok(result);
        });
    }

    [HttpPost("email-change-requests")]
    public async Task<IActionResult> RequestEmailChange(RequestEmailChangeDto request)
    {
        try
        {
            await _clientPortalService.RequestEmailChangeAsync(request);
            return Accepted();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpGet("dashboard")]
    public async Task<ActionResult<ClientPortalDashboardDto>> GetDashboard()
    {
        var result = await _clientPortalService.GetDashboardAsync();

        if (result is null)
            return NotFound();

        return Ok(result);
    }

    [HttpGet("schedule")]
    public async Task<ActionResult<List<ClientPortalSessionDto>>> GetSchedule()
    {
        return Ok(await _clientPortalService.GetScheduleAsync());
    }

    [HttpGet("subscription")]
    public async Task<ActionResult<SubscriptionDto>> GetSubscription()
    {
        return await HandleAsync<SubscriptionDto>(async () =>
            Ok(await _subscriptionService.GetCurrentClientSubscriptionAsync()));
    }

    [HttpPost("subscription/cancel-request")]
    public async Task<ActionResult<SubscriptionDto>> RequestCancelSubscription()
    {
        return await HandleAsync<SubscriptionDto>(async () =>
            Ok(await _subscriptionService.RequestCancelRenewalAsClientAsync()));
    }

    [HttpDelete("subscription/cancel-request")]
    public async Task<ActionResult<SubscriptionDto>> WithdrawCancelSubscriptionRequest()
    {
        return await HandleAsync<SubscriptionDto>(async () =>
            Ok(await _subscriptionService.WithdrawCancelRenewalRequestAsClientAsync()));
    }

    [HttpGet("subscription/current-cycle/usage")]
    public async Task<ActionResult<SubscriptionUsageDto>> GetSubscriptionUsage()
    {
        return await HandleAsync<SubscriptionUsageDto>(async () =>
            Ok(await _subscriptionService.GetCurrentClientUsageAsync()));
    }

    [HttpGet("training-plan")]
    public async Task<ActionResult<TrainingPlanDto>> GetTrainingPlan()
    {
        return await HandleAsync<TrainingPlanDto>(async () =>
            Ok(await _subscriptionService.GetCurrentClientTrainingPlanAsync()));
    }

    [HttpGet("billing")]
    public async Task<ActionResult<ClientBillingSummaryDto>> GetBilling()
    {
        return await HandleAsync<ClientBillingSummaryDto>(async () =>
            Ok(await _clientPaymentService.GetCurrentClientSummaryAsync()));
    }

    [HttpPost("payments")]
    public async Task<ActionResult<ClientPaymentDto>> RequestPayment(CreateClientPaymentRequest request)
    {
        return await HandleAsync<ClientPaymentDto>(async () =>
        {
            var payment = await _clientPaymentService.RequestPaymentAsClientAsync(request);
            return CreatedAtAction(nameof(GetBilling), new { id = payment.Id }, payment);
        });
    }

    [HttpGet("trainer-contact")]
    public async Task<IActionResult> GetTrainerContact()
    {
        var userId = GetCurrentUserId();

        if (userId is null)
            return Unauthorized();

        var contact = await _clientPortalService.GetTrainerContactAsync(userId.Value);

        if (contact is null)
        {
            return NotFound(new
            {
                message = "Brak przypisanego trenera dla tego klienta."
            });
        }

        return Ok(contact);
    }

    [HttpGet("owner-contact")]
    public async Task<IActionResult> GetOwnerContact()
    {
        var contact = await _clientPortalService.GetOwnerContactAsync();

        if (contact is null)
        {
            return NotFound(new
            {
                message = "Brak aktywnego właściciela studia do kontaktu."
            });
        }

        return Ok(contact);
    }

    [HttpGet("milestones")]
    public async Task<IActionResult> GetMyMilestones()
    {
        var userId = GetCurrentUserId();

        if (userId is null)
            return Unauthorized();

        var client = await _context.Clients
            .FirstOrDefaultAsync(c => c.UserId == userId.Value);

        if (client is null)
        {
            return NotFound(new
            {
                message = "Nie znaleziono profilu klienta dla zalogowanego użytkownika."
            });
        }

        var milestones = await _milestoneService.GetClientMilestonesAsync(client.Id);

        if (milestones is null)
            return NotFound();

        return Ok(milestones);
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
            return BadRequest(new
            {
                message = ex.Message
            });
        }
    }
}
