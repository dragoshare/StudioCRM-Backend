using Microsoft.AspNetCore.Mvc;
using StudioCRM.Application.DTOs.Packages;
using StudioCRM.Application.Interfaces;

namespace StudioCRM.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PackagesController : ControllerBase
{
    private readonly IPackageService _packageService;

    public PackagesController(IPackageService packageService)
    {
        _packageService = packageService;
    }

    [HttpGet]
    public async Task<ActionResult<List<PackageDto>>> GetAll()
    {
        return Ok(await _packageService.GetAllAsync());
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<PackageDto>> GetById(int id)
    {
        var result = await _packageService.GetByIdAsync(id);
        if (result is null) return NotFound();
        return Ok(result);
    }

    [HttpPost]
    public async Task<ActionResult<PackageDto>> Create(CreatePackageDto request)
    {
        var result = await _packageService.CreateAsync(request);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<PackageDto>> Update(int id, UpdatePackageDto request)
    {
        var result = await _packageService.UpdateAsync(id, request);
        if (result is null) return NotFound();
        return Ok(result);
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var deleted = await _packageService.DeleteAsync(id);
        if (!deleted) return NotFound();
        return NoContent();
    }

    [HttpPost("{id:int}/restore")]
    public async Task<IActionResult> Restore(int id)
    {
        var restored = await _packageService.RestoreAsync(id);
        if (!restored) return NotFound();
        return NoContent();
    }

    [HttpGet("deleted")]
    public async Task<ActionResult<List<PackageDto>>> GetDeleted()
    {
        return Ok(await _packageService.GetDeletedAsync());
    }
}