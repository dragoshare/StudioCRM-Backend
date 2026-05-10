using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StudioCRM.Application.DTOs.Clients;
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

    public ClientsController(
        IClientService clientService,
        ISubscriptionService subscriptionService)
    {
        _clientService = clientService;
        _subscriptionService = subscriptionService;
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

    [HttpPut("{id:int}")]
    public async Task<ActionResult<ClientDto>> Update(int id, UpdateClientDto request)
    {
        var result = await _clientService.UpdateAsync(id, request);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpPost("{id:int}/deactivate")]
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

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        return await Deactivate(id);
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
    public async Task<ActionResult<List<ClientDto>>> GetDeleted()
    {
        return Ok(await _clientService.GetDeletedAsync());
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
}
