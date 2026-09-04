using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StudioCRM.Application.DTOs.Auth;
using StudioCRM.Application.DTOs.Public;
using StudioCRM.Application.Interfaces;

namespace StudioCRM.Api.Controllers;

[ApiController]
[Route("api/public/group-classes")]
public class PublicGroupClassesController : ControllerBase
{
    private readonly IPublicGroupClassService _publicGroupClassService;
    private readonly IAuthService _authService;

    public PublicGroupClassesController(
        IPublicGroupClassService publicGroupClassService,
        IAuthService authService)
    {
        _publicGroupClassService = publicGroupClassService;
        _authService = authService;
    }

    [AllowAnonymous]
    [HttpGet("locations")]
    public async Task<ActionResult<List<PublicGroupLocationDto>>> GetLocations()
    {
        return Ok(await _publicGroupClassService.GetLocationsAsync());
    }

    [AllowAnonymous]
    [HttpGet("packages")]
    public async Task<ActionResult<List<PublicGroupPackageDto>>> GetPackages([FromQuery] int? locationId)
    {
        return Ok(await _publicGroupClassService.GetPackagesAsync(locationId));
    }

    [AllowAnonymous]
    [HttpGet("packages/by-slug/{slug}")]
    public async Task<ActionResult<PublicGroupPackageDto>> GetPackageBySlug(string slug)
    {
        var result = await _publicGroupClassService.GetPackageBySlugAsync(slug);
        return result is null ? NotFound() : Ok(result);
    }

    [AllowAnonymous]
    [HttpGet]
    public async Task<ActionResult<List<PublicGroupClassDto>>> GetClasses([FromQuery] PublicGroupClassFilterDto filter)
    {
        return Ok(await _publicGroupClassService.GetClassesAsync(filter));
    }

    [AllowAnonymous]
    [HttpGet("{id:int}")]
    public async Task<ActionResult<PublicGroupClassDto>> GetClass(int id)
    {
        var result = await _publicGroupClassService.GetClassAsync(id);
        return result is null ? NotFound() : Ok(result);
    }

    [AllowAnonymous]
    [HttpGet("by-slug/{slug}")]
    public async Task<ActionResult<PublicGroupClassDto>> GetClassBySlug(string slug)
    {
        var result = await _publicGroupClassService.GetClassBySlugAsync(slug);
        return result is null ? NotFound() : Ok(result);
    }

    [AllowAnonymous]
    [HttpPost("register")]
    public async Task<ActionResult<AuthResponseDto>> Register(PublicGroupRegisterRequest request)
    {
        return await HandleAsync<AuthResponseDto>(async () =>
            Ok(await _authService.RegisterPublicGroupClientAsync(request)));
    }

    [Authorize(Roles = "Client")]
    [HttpPost("packages/{packageId:int}/purchases/me")]
    public async Task<ActionResult<PublicGroupPurchaseDto>> PurchasePackage(int packageId)
    {
        return await HandleAsync<PublicGroupPurchaseDto>(async () =>
            Ok(await _publicGroupClassService.PurchasePackageForCurrentClientAsync(packageId)));
    }

    [Authorize(Roles = "Client")]
    [HttpPost("{sessionId:int}/bookings/me")]
    public async Task<ActionResult<PublicGroupBookingDto>> BookMe(int sessionId)
    {
        return await HandleAsync<PublicGroupBookingDto>(async () =>
            Ok(await _publicGroupClassService.BookCurrentClientAsync(sessionId)));
    }

    [Authorize(Roles = "Client")]
    [HttpDelete("{sessionId:int}/bookings/me")]
    public async Task<IActionResult> CancelMyBooking(int sessionId)
    {
        return await HandleAsync(async () =>
        {
            var cancelled = await _publicGroupClassService.CancelCurrentClientBookingAsync(sessionId);
            return cancelled ? NoContent() : NotFound();
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
            return BadRequest(new { message = ex.Message });
        }
    }

    private async Task<IActionResult> HandleAsync(Func<Task<IActionResult>> action)
    {
        try
        {
            return await action();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }
}
