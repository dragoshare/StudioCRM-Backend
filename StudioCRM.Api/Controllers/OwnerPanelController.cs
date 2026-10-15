using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StudioCRM.Application.DTOs.Billing;
using StudioCRM.Application.DTOs.Clients;
using StudioCRM.Application.DTOs.TrainerSettlements;
using StudioCRM.Application.Interfaces;

namespace StudioCRM.Api.Controllers;

// Aggregated reads for the Owner panel. Contract: docs/BACKEND_FOR_FRONTEND_OPTIMIZATION.md.
[ApiController]
[Route("api/owner")]
[Authorize(Roles = "Owner")]
public class OwnerPanelController(
    ITrainerSettlementService settlements,
    IClientService clients,
    IClientPaymentService payments) : ControllerBase
{
    [HttpGet("trainer-settlements")]
    public async Task<ActionResult<TrainerSettlementListDto>> TrainerSettlements([FromQuery] TrainerSettlementListFilter filter) =>
        Ok(await settlements.GetMonthlySettlementsAsync(filter));

    [HttpGet("filter-options")]
    public async Task<ActionResult<OwnerFilterOptionsDto>> FilterOptions([FromQuery] OwnerFilterOptionsQuery query) =>
        Ok(await clients.GetFilterOptionsAsync(query));

    [HttpGet("operations/{key}")]
    [ProducesResponseType(typeof(PaymentOperationStatusDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Operation(string key)
    {
        if (!Guid.TryParse(key, out var parsed) || parsed == Guid.Empty)
            return BadRequest(new { message = "Operation key must be a non-empty UUID." });
        var result = await payments.GetStaffPaymentOperationAsync(parsed);
        // NotFound: nothing was committed under this key; retrying the original request with the same key is safe.
        return result is null ? NotFound(new { state = "NotFound", message = "No operation was committed with this key." }) : Ok(result);
    }
}
