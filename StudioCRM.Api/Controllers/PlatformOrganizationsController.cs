using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StudioCRM.Application.Interfaces;
using StudioCRM.Application.DTOs.Branding;
namespace StudioCRM.Api.Controllers;
[ApiController]
[Authorize(Roles = "SuperAdmin")]
[Route("api/super-admin/organizations")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public class PlatformOrganizationsController(IPlatformOrganizationService service) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(int page = 1, int pageSize = 25) => Ok(await service.ListAsync(page, pageSize));
    [HttpGet("{organizationId:guid}")]
    public async Task<IActionResult> Get(Guid organizationId) => Ok(await service.GetAsync(organizationId));
    [HttpGet("{organizationId:guid}/branding/draft")]
    public async Task<IActionResult> Draft(Guid organizationId) => Ok(await (await service.BrandingAsync(organizationId)).GetDraftAsync());
    [HttpPut("{organizationId:guid}/branding/draft")]
    public async Task<IActionResult> Save(Guid organizationId, SaveBrandingDraftRequest request) => Ok(await (await service.BrandingAsync(organizationId)).SaveDraftAsync(request));
    [HttpPost("{organizationId:guid}/branding/publish")]
    public async Task<IActionResult> Publish(Guid organizationId, BrandingChangeRequest request) => Ok(await (await service.BrandingAsync(organizationId)).PublishAsync(request));
    [HttpPost("{organizationId:guid}/branding/restore")]
    public async Task<IActionResult> Restore(Guid organizationId, RestoreBrandingRequest request) => Ok(await (await service.BrandingAsync(organizationId)).RestoreDraftAsync(request));
    [HttpGet("{organizationId:guid}/branding/versions")]
    public async Task<IActionResult> Versions(Guid organizationId, int page = 1, int pageSize = 25) => Ok(await (await service.BrandingAsync(organizationId)).GetVersionsAsync(page, pageSize));
    [HttpGet("{organizationId:guid}/branding/audit")]
    public async Task<IActionResult> Audit(Guid organizationId, int page = 1, int pageSize = 25) => Ok(await (await service.BrandingAsync(organizationId)).GetAuditAsync(page, pageSize));
    [HttpPost("{organizationId:guid}/branding/assets")]
    [RequestSizeLimit(6 * 1024 * 1024)]
    public async Task<IActionResult> Upload(Guid organizationId, IFormFile file, CancellationToken ct)
    {
        var branding = await service.BrandingAsync(organizationId);
        if (file.Length == 0 || file.Length > 5 * 1024 * 1024) return BadRequest(new { message = "PNG must be 1 byte to 5 MiB." });
        await using var stream = file.OpenReadStream();
        return Ok(await branding.UploadAsync(stream, ct));
    }
}
[ApiController]
[Route("api/organization")]
public class CurrentOrganizationController(IPlatformOrganizationService service) : ControllerBase
{
    [HttpGet, AllowAnonymous]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<IActionResult> Get() => Ok(await service.GetCurrentAsync());
}
