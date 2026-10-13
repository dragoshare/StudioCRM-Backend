using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StudioCRM.Infrastructure.Services;
namespace StudioCRM.Api.Controllers;
[ApiController]
[Authorize(Roles = "Owner,Client")]
[Route("api/clients/{clientId:int}/period-packages")]
public class PeriodPackagesController(PeriodPackageService service) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(int clientId, int page = 1, int pageSize = 25) => Ok(await service.ListAsync(clientId, page, pageSize));
    [HttpPatch("{clientPackageId:int}/renewal")]
    [Authorize(Roles = "Owner")]
    public async Task<IActionResult> Renewal(int clientId, int clientPackageId, RenewalRequest request)
    { await service.SetRenewalAsync(clientId, clientPackageId, request.Enabled); return NoContent(); }
    public record RenewalRequest(bool Enabled);
}
