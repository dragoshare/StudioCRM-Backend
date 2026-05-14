using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StudioCRM.Application.DTOs.Packages;
using StudioCRM.Application.Interfaces;

namespace StudioCRM.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class PackagesController : ControllerBase
{
    private readonly IPackageService _packageService;

    public PackagesController(IPackageService packageService)
    {
        _packageService = packageService;
    }

    [HttpGet]
    [Authorize(Roles = "Owner,Trainer")]
    public async Task<ActionResult<List<PackageDto>>> GetAll()
    {
        return Ok(await _packageService.GetAllAsync());
    }

    [HttpGet("{id:int}")]
    [Authorize(Roles = "Owner,Trainer")]
    public async Task<ActionResult<PackageDto>> GetById(int id)
    {
        var result = await _packageService.GetByIdAsync(id);
        if (result is null) return NotFound();
        return Ok(result);
    }

    [HttpPost]
    [Authorize(Roles = "Owner")]
    public async Task<ActionResult<PackageDto>> Create(CreatePackageDto request)
    {
        try
        {
            var result = await _packageService.CreateAsync(request);
            return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPut("{id:int}")]
    [Authorize(Roles = "Owner")]
    public async Task<ActionResult<PackageDto>> Update(int id, UpdatePackageDto request)
    {
        try
        {
            var result = await _packageService.UpdateAsync(id, request);
            if (result is null) return NotFound();
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpDelete("{id:int}")]
    [Authorize(Roles = "Owner")]
    public async Task<IActionResult> Delete(int id)
    {
        try
        {
            var deleted = await _packageService.DeleteAsync(id);
            if (!deleted) return NotFound();
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { message = ex.Message });
        }
        catch (DbUpdateException)
        {
            return Conflict(new
            {
                message = "Package cannot be deleted because it is still connected with existing data."
            });
        }
    }

    [HttpPost("{id:int}/restore")]
    [Authorize(Roles = "Owner")]
    public async Task<IActionResult> Restore(int id)
    {
        try
        {
            var restored = await _packageService.RestoreAsync(id);
            if (!restored) return NotFound();
            return NoContent();
        }
        catch (DbUpdateException)
        {
            return Conflict(new
            {
                message = "Package cannot be restored because of a database constraint conflict."
            });
        }
    }

    [HttpGet("deleted")]
    [Authorize(Roles = "Owner")]
    public async Task<ActionResult<List<PackageDto>>> GetDeleted()
    {
        return Ok(await _packageService.GetDeletedAsync());
    }
}
