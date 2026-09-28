using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StudioCRM.Application.DTOs.Billing;
using StudioCRM.Application.DTOs.Clients;
using StudioCRM.Application.DTOs.Profiles;
using StudioCRM.Application.DTOs.Subscriptions;
using StudioCRM.Application.DTOs.TrainingPlans;
using StudioCRM.Application.Interfaces;

namespace StudioCRM.Api.Controllers;

[ApiController]
[Route("api/clients")]
[Authorize(Roles = "Owner")]
public class ClientsController : ControllerBase
{
    private readonly IClientService _clientService;
    private readonly ISubscriptionService _subscriptionService;
    private readonly IClientPaymentService _clientPaymentService;
    private readonly IAvatarService _avatarService;

    public ClientsController(
        IClientService clientService,
        ISubscriptionService subscriptionService,
        IClientPaymentService clientPaymentService,
        IAvatarService avatarService)
    {
        _clientService = clientService;
        _subscriptionService = subscriptionService;
        _clientPaymentService = clientPaymentService;
        _avatarService = avatarService;
    }

    [HttpGet]
    public async Task<ActionResult<List<ClientDto>>> GetAll()
    {
        return Ok(await _clientService.GetAllAsync());
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<ClientDto>> GetById(int id)
    {
        var result = await _clientService.GetByIdAsync(id);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpGet("{id:int}/workspace")]
    public async Task<ActionResult<ClientWorkspaceDto>> GetWorkspace(int id)
    {
        return await HandleAsync<ClientWorkspaceDto>(async () =>
        {
            var result = await _clientService.GetWorkspaceAsync(id);
            return result is null ? NotFound() : Ok(result);
        });
    }

    [HttpGet("filter")]
    public async Task<ActionResult<List<ClientDto>>> Filter([FromQuery] ClientFilterDto filter)
    {
        return Ok(await _clientService.GetFilteredAsync(filter));
    }

    [HttpPost]
    public async Task<ActionResult<ClientDto>> Create(CreateClientDto request)
    {
        var result = await _clientService.CreateAsync(request);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    [HttpPatch("{id:int}")]
    public async Task<ActionResult<ClientDto>> Patch(int id, UpdateClientDto request)
    {
        var result = await _clientService.UpdateAsync(id, request);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpGet("{id:int}/legal-consents")]
    public async Task<ActionResult<List<ClientLegalConsentDto>>> GetLegalConsents(int id)
    {
        return Ok(await _clientService.GetLegalConsentsAsync(id));
    }

    [HttpGet("{id:int}/locations")]
    public async Task<IActionResult> GetLocations(int id) => Ok(await _clientService.GetLocationsAsync(id));

    [HttpPut("{id:int}/locations/{locationId:int}")]
    public async Task<IActionResult> SetLocationAccess(int id, int locationId, SetClientLocationAccessRequest request)
    {
        await _clientService.SetLocationAccessAsync(id, locationId, request);
        return NoContent();
    }

    [HttpDelete("{id:int}/locations/{locationId:int}")]
    public async Task<IActionResult> RemoveLocation(int id, int locationId)
    {
        await _clientService.RemoveLocationAsync(id, locationId);
        return NoContent();
    }

    [HttpGet("duplicates")]
    public async Task<IActionResult> FindDuplicates([FromQuery] ClientDuplicateFilter filter) => Ok(await _clientService.FindDuplicatesAsync(filter));

    [HttpGet("{id:int}/packages/history")]
    public async Task<IActionResult> GetPackageHistory(int id, int page = 1, int pageSize = 25)
        => Ok(await _clientService.GetPackageHistoryAsync(id, page, pageSize));

    [HttpGet("refunds")]
    public async Task<IActionResult> GetRefunds(int? clientId, int page = 1, int pageSize = 25)
        => Ok(await _clientService.GetRefundsAsync(clientId, page, pageSize));

    [HttpGet("{id:int}/sessions")]
    public async Task<IActionResult> GetSessionHistory(int id, [FromQuery] ClientHistoryFilter filter)
        => Ok(await _clientService.GetSessionHistoryAsync(id, filter));

    [HttpGet("{id:int}/audit")]
    public async Task<IActionResult> GetAudit(int id, int page = 1, int pageSize = 25)
        => Ok(await _clientService.GetAuditAsync(id, page, pageSize));

    [HttpGet("{id:int}/closure-preview")]
    public async Task<IActionResult> GetClosurePreview(int id) => Ok(await _clientService.GetClosurePreviewAsync(id));

    [HttpPost("{id:int}/close-cooperation")]
    public async Task<IActionResult> CloseCooperation(int id, CloseClientRequest request)
        => Ok(await _clientService.CloseCooperationAsync(id, request));

    [HttpPost("{id:int}/packages/{packageId:int}/confirm-refund")]
    public async Task<IActionResult> ConfirmRefund(int id, int packageId, ConfirmClientRefundRequest request)
    {
        await _clientService.ConfirmRefundAsync(id, packageId, request);
        return NoContent();
    }

    [HttpPost("{id:int}/packages/{packageId:int}/resume-retained")]
    public async Task<IActionResult> ResumeRetained(int id, int packageId, ResumeRetainedPackageRequest request)
    {
        await _clientService.ResumeRetainedPackageAsync(id, packageId, request);
        return NoContent();
    }

    [HttpPost("{id:int}/avatar")]
    [Consumes("multipart/form-data")]
    public async Task<ActionResult<AvatarDto>> UploadAvatar(
        int id,
        IFormFile file,
        CancellationToken cancellationToken)
    {
        if (file is null)
            return BadRequest(new { message = "Avatar file is required." });

        return await HandleAsync<AvatarDto>(async () =>
        {
            await using var stream = file.OpenReadStream();
            return Ok(await _avatarService.UploadClientAvatarAsync(
                id,
                stream,
                file.FileName,
                file.ContentType,
                file.Length,
                cancellationToken));
        });
    }

    [HttpDelete("{id:int}/avatar")]
    public async Task<ActionResult<AvatarDto>> DeleteAvatar(
        int id,
        CancellationToken cancellationToken)
    {
        return await HandleAsync<AvatarDto>(async () =>
            Ok(await _avatarService.DeleteClientAvatarAsync(id, cancellationToken)));
    }

    [HttpPost("{id:int}/deactivate")]
    [HttpPost("{id:int}/archive")]
    public async Task<IActionResult> Deactivate(int id)
    {
        try
        {
            var deleted = await _clientService.DeleteAsync(id);
            return deleted ? NoContent() : NotFound();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("{id:int}/restore")]
    public async Task<IActionResult> Restore(int id)
    {
        var result = await _clientService.RestoreAsync(id);
        return result ? NoContent() : NotFound();
    }

    [HttpPatch("{id:int}/trainer")]
    public async Task<IActionResult> AssignTrainer(int id, SetClientTrainerRequest request)
    {
        try
        {
            var result = await _clientService.AssignTrainerAsync(id, request);
            return result ? NoContent() : NotFound();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpGet("{id:int}/subscription")]
    public async Task<ActionResult<SubscriptionDto>> GetSubscription(int id)
    {
        return await HandleAsync<SubscriptionDto>(async () =>
            Ok(await _subscriptionService.GetClientSubscriptionAsync(id)));
    }

    [HttpPut("{id:int}/subscription/next-package")]
    public async Task<ActionResult<SubscriptionDto>> SetNextPackage(
        int id,
        SetNextPackageRequest request)
    {
        return await HandleAsync<SubscriptionDto>(async () =>
            Ok(await _subscriptionService.SetNextPackageAsync(id, request)));
    }

    [HttpPost("{id:int}/subscription/cancel")]
    public async Task<ActionResult<SubscriptionDto>> CancelRenewal(int id)
    {
        return await HandleAsync<SubscriptionDto>(async () =>
            Ok(await _subscriptionService.CancelRenewalAsync(id)));
    }

    [HttpPost("{id:int}/subscription/resume")]
    public async Task<ActionResult<SubscriptionDto>> ResumeRenewal(int id)
    {
        return await HandleAsync<SubscriptionDto>(async () =>
            Ok(await _subscriptionService.ResumeRenewalAsync(id)));
    }

    [HttpGet("{id:int}/subscription/current-cycle/usage")]
    public async Task<ActionResult<SubscriptionUsageDto>> GetUsage(int id)
    {
        return await HandleAsync<SubscriptionUsageDto>(async () =>
            Ok(await _subscriptionService.GetClientUsageAsync(id)));
    }

    [HttpGet("{id:int}/subscription/current-cycle")]
    public async Task<ActionResult<ClientPackageBillingDto>> GetCurrentCycle(int id)
    {
        return await HandleAsync<ClientPackageBillingDto>(async () =>
        {
            var result = await _clientPaymentService.GetActivePackageAsync(id);
            return result is null ? NotFound() : Ok(result);
        });
    }

    [HttpGet("{id:int}/balance-transactions")]
    public async Task<ActionResult<PagedResultDto<ClientBalanceTransactionDto>>> GetBalanceTransactions(
        int id,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25)
    {
        return await HandleAsync<PagedResultDto<ClientBalanceTransactionDto>>(async () =>
            Ok(await _clientPaymentService.GetClientBalanceTransactionsAsync(id, page, pageSize)));
    }

    [HttpGet("{id:int}/training-plan")]
    public async Task<ActionResult<TrainingPlanDto>> GetTrainingPlan(int id)
    {
        return await HandleAsync<TrainingPlanDto>(async () =>
            Ok(await _subscriptionService.GetClientTrainingPlanAsync(id)));
    }

    [HttpPut("{id:int}/training-plan")]
    public async Task<ActionResult<TrainingPlanDto>> UpdateTrainingPlan(
        int id,
        UpdateTrainingPlanRequest request)
    {
        return await HandleAsync<TrainingPlanDto>(async () =>
            Ok(await _subscriptionService.UpdateTrainingPlanAsync(id, request)));
    }

    [HttpGet("deleted")]
    [HttpGet("archived")]
    public async Task<ActionResult<List<ClientDto>>> GetDeleted()
    {
        return Ok(await _clientService.GetDeletedAsync());
    }

    [HttpGet("{id:int}/archive-check")]
    public async Task<ActionResult<ClientArchiveCheckDto>> CheckArchive(int id)
        => Ok(await _clientService.CheckArchiveAsync(id));

    [HttpPost("archive-batch")]
    public async Task<ActionResult<List<ClientArchiveResultDto>>> ArchiveMany(BulkArchiveClientsRequest request)
        => Ok(await _clientService.ArchiveManyAsync(request));

    [HttpPut("{id:int}/portal-access")]
    public async Task<IActionResult> SetPortalAccess(int id, SetClientPortalAccessRequest request)
        => await _clientService.SetPortalAccessAsync(id, request.Blocked) ? NoContent() : NotFound();

    [HttpDelete("{id:int}/permanent")]
    public async Task<IActionResult> DeletePermanently(int id)
        => await _clientService.DeletePermanentlyAsync(id) ? NoContent() : NotFound();

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
}
