using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StudioCRM.Application.DTOs.Branding;
using StudioCRM.Application.Interfaces;

namespace StudioCRM.Api.Controllers;

[ApiController]
[Route("api/organization/branding")]
public class BrandingController(IBrandingService service) : ControllerBase
{
    [HttpGet, AllowAnonymous]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<ActionResult<PublicBrandingDto>> Get() => Ok(await service.GetPublishedAsync());
}

[ApiController]
[Route("api/super-admin/organization/branding")]
[Authorize(Roles = "SuperAdmin")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public class AdminBrandingController(IBrandingService service) : ControllerBase
{
    [HttpGet("draft")]
    public async Task<IActionResult> GetDraft() => Ok(await service.GetDraftAsync());
    [HttpPut("draft")]
    public async Task<IActionResult> Save(SaveBrandingDraftRequest request) => Ok(await service.SaveDraftAsync(request));
    [HttpPost("publish")]
    public async Task<IActionResult> Publish(BrandingChangeRequest request) => Ok(await service.PublishAsync(request));
    [HttpPost("restore")]
    public async Task<IActionResult> Restore(RestoreBrandingRequest request) => Ok(await service.RestoreDraftAsync(request));
    [HttpGet("versions")]
    public async Task<IActionResult> Versions(int page = 1, int pageSize = 25) => Ok(await service.GetVersionsAsync(page, pageSize));
    [HttpGet("audit")]
    public async Task<IActionResult> Audit(int page = 1, int pageSize = 25) => Ok(await service.GetAuditAsync(page, pageSize));
    [HttpPost("assets")]
    [RequestSizeLimit(6 * 1024 * 1024)]
    public async Task<IActionResult> Upload(IFormFile file, CancellationToken ct)
    {
        if (file.Length == 0 || file.Length > 5 * 1024 * 1024) return BadRequest(new { message = "PNG must be 1 byte to 5 MiB." });
        await using var stream = file.OpenReadStream();
        return Ok(await service.UploadAsync(stream, ct));
    }
}
