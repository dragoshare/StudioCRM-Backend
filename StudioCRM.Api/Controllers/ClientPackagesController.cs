using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StudioCRM.Application.DTOs.ClientPackages;
using StudioCRM.Application.Interfaces;

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
    }
}
