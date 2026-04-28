using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StudioCRM.Application.DTOs.ClientPortal;
using StudioCRM.Application.Interfaces;
using StudioCRM.Infrastructure.Persistence;

namespace StudioCRM.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Client")]
public class ClientPortalController : ControllerBase
{
    private readonly IClientPortalService _clientPortalService;
    private readonly IMilestoneService _milestoneService;
    private readonly StudioCRMDbContext _context;

    public ClientPortalController(
        IClientPortalService clientPortalService,
        IMilestoneService milestoneService,
        StudioCRMDbContext context)
    {
        _clientPortalService = clientPortalService;
        _milestoneService = milestoneService;
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

    [HttpGet("package")]
    public async Task<ActionResult<ClientPortalPackageDto>> GetPackage()
    {
        var result = await _clientPortalService.GetPackageAsync();

        if (result is null)
            return NotFound();

        return Ok(result);
    }

    [HttpGet("payments")]
    public async Task<ActionResult<ClientPortalPaymentDto>> GetPayments()
    {
        var result = await _clientPortalService.GetPaymentAsync();

        if (result is null)
            return NotFound();

        return Ok(result);
    }

    [HttpGet("trainer")]
    public async Task<ActionResult<ClientPortalTrainerDto>> GetTrainer()
    {
        var result = await _clientPortalService.GetTrainerAsync();

        if (result is null)
            return NotFound();

        return Ok(result);
    }

    [HttpGet("package-settlement")]
    public async Task<ActionResult<ClientPackageSettlementDto>> GetPackageSettlement()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (string.IsNullOrEmpty(userId))
            return Unauthorized();

        var result = await _clientPortalService.GetPackageSettlementAsync(userId);

        return Ok(result);
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
}