using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StudioCRM.Application.DTOs.Dashboard;
using StudioCRM.Infrastructure.Services;
namespace StudioCRM.Api.Controllers;
[ApiController]
[Authorize(Roles = "Owner")]
[Route("api/owner/dashboard")]
public class OwnerOverviewController(OwnerOverviewService service) : ControllerBase
{
    [HttpGet("summary")]
    public async Task<ActionResult<OwnerOverviewSummaryDto>> Summary(string? period = null, int? locationId = null) => Ok(await service.SummaryAsync(period, locationId));
    [HttpGet("revenue")]
    public async Task<ActionResult<OwnerRevenueDto>> Revenue(int months = 12, string? period = null, int? locationId = null) => Ok(await service.RevenueAsync(months, period, locationId));
    [HttpGet("recent-clients")]
    public async Task<ActionResult<List<RecentOwnerClientDto>>> RecentClients(int limit = 4, int? locationId = null) => Ok(await service.RecentClientsAsync(limit, locationId));
    [HttpGet("attention")]
    public async Task<ActionResult<OwnerAttentionDto>> Attention(int? locationId = null) => Ok(await service.AttentionAsync(locationId));
}
