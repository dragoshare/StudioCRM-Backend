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
    public async Task<ActionResult<OwnerOverviewSummaryDto>> Summary(string? period = null) => Ok(await service.SummaryAsync(period));
    [HttpGet("revenue")]
    public async Task<ActionResult<OwnerRevenueDto>> Revenue(int months = 12, string? period = null) => Ok(await service.RevenueAsync(months, period));
    [HttpGet("recent-clients")]
    public async Task<ActionResult<List<RecentOwnerClientDto>>> RecentClients(int limit = 4) => Ok(await service.RecentClientsAsync(limit));
    [HttpGet("attention")]
    public async Task<ActionResult<OwnerAttentionDto>> Attention() => Ok(await service.AttentionAsync());
}
