using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StudioCRM.Application.DTOs.ClientPackages;
using StudioCRM.Application.Interfaces;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace StudioCRM.Api.Controllers;

[ApiController]
[Route("api/client-packages")]
[Authorize(Roles = "Owner")]
public class ClientPackagesController : ControllerBase
{
    private readonly IClientPackageService _clientPackageService;

    public ClientPackagesController(IClientPackageService clientPackageService)
    {
        _clientPackageService = clientPackageService;
    }

    [HttpPost]
    public async Task<ActionResult<object>> Create(CreateClientPackageRequest request)
    {
        return await HandleAsync<object>(async () =>
        {
            var id = await _clientPackageService.CreateAsync(request);
            return CreatedAtAction(nameof(Create), new { id }, new { id });
        });
    }

    [HttpPost("clients/{clientId:int}/packages/{clientPackageId:int}/activate")]
    public async Task<IActionResult> Activate(int clientId, int clientPackageId)
    {
        try
        {
            var activated = await _clientPackageService.ActivateAsync(clientId, clientPackageId);

            if (!activated)
                return NotFound();

            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new
            {
                message = ex.Message
            });
        }
    }

    [HttpDelete("clients/{clientId:int}/packages/{clientPackageId:int}")]
    public async Task<IActionResult> Delete(int clientId, int clientPackageId)
    {
        try
        {
            var deleted = await _clientPackageService.DeleteAsync(clientId, clientPackageId);

            if (!deleted)
                return NotFound();

            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new
            {
                message = ex.Message
            });
        }
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
        catch (Exception ex) when (ex is PostgresException { SqlState: "40001" or "40P01" } ||
            ex is DbUpdateException { InnerException: PostgresException { SqlState: "40001" or "40P01" } })
        {
            return Conflict(new { message = "Concurrent change detected. Refresh the package preview before retrying." });
        }
    }

    [HttpGet("clients/{clientId:int}/packages/{clientPackageId:int}/management-preview")]
    public Task<ActionResult<PackageManagementPreviewDto>> Preview(int clientId, int clientPackageId)
        => HandleAsync<PackageManagementPreviewDto>(async () => Ok(await _clientPackageService.GetManagementPreviewAsync(clientId, clientPackageId)));

    [HttpPost("clients/{clientId:int}/packages/{clientPackageId:int}/correct")]
    public Task<ActionResult<object>> Correct(int clientId, int clientPackageId, PackageChangeRequest request)
        => HandleAsync<object>(async () => { await _clientPackageService.CorrectAsync(clientId, clientPackageId, request); return Ok(new { corrected = true }); });

    [HttpPost("clients/{clientId:int}/packages/{clientPackageId:int}/close")]
    public Task<ActionResult<object>> Close(int clientId, int clientPackageId, ClosePackageRequest request)
        => HandleAsync<object>(async () => Ok(new { replacementClientPackageId = await _clientPackageService.CloseAsync(clientId, clientPackageId, request) }));

    [HttpPost("import")]
    public Task<ActionResult<object>> Import(ImportClientPackageRequest request)
        => HandleAsync<object>(async () => Ok(new { id = await _clientPackageService.ImportAsync(request) }));
}
