using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using StudioCRM.Application.DTOs.Billing;
using StudioCRM.Application.DTOs.Clients;
using StudioCRM.Application.DTOs.ClientPackages;
using StudioCRM.Application.DTOs.Invitations;
using StudioCRM.Application.DTOs.Profiles;
using StudioCRM.Application.DTOs.SessionParticipants;
using StudioCRM.Application.DTOs.Sessions;
using StudioCRM.Application.DTOs.Subscriptions;
using StudioCRM.Application.DTOs.TrainerPortal;
using StudioCRM.Application.DTOs.TrainerSettlements;
using StudioCRM.Application.DTOs.TrainingPlans;
using StudioCRM.Application.Interfaces;

namespace StudioCRM.Api.Controllers;

[ApiController]
[Route("api/trainer-portal")]
[Authorize(Roles = "Owner,Trainer")]
public class TrainerPortalController : ControllerBase
{
    private readonly ITrainerPortalService _trainerPortalService;
    private readonly IClientPaymentService _clientPaymentService;
    private readonly IClientPackageService _clientPackageService;
    private readonly ISubscriptionService _subscriptionService;
    private readonly IInvitationService _invitationService;
    private readonly ISessionParticipantService _sessionParticipantService;
    private readonly ISessionService _sessionService;
    private readonly IAvatarService _avatarService;

    public TrainerPortalController(
        ITrainerPortalService trainerPortalService,
        IClientPaymentService clientPaymentService,
        IClientPackageService clientPackageService,
        ISubscriptionService subscriptionService,
        IInvitationService invitationService,
        ISessionParticipantService sessionParticipantService,
        ISessionService sessionService,
        IAvatarService avatarService)
    {
        _trainerPortalService = trainerPortalService;
        _clientPaymentService = clientPaymentService;
        _clientPackageService = clientPackageService;
        _subscriptionService = subscriptionService;
        _invitationService = invitationService;
        _sessionParticipantService = sessionParticipantService;
        _sessionService = sessionService;
        _avatarService = avatarService;
    }

    [HttpGet("me")]
    public async Task<ActionResult<TrainerPortalMeDto>> GetMe()
    {
        var result = await _trainerPortalService.GetMeAsync();
        return result is null ? NotFound() : Ok(result);
    }

    [HttpPatch("me")]
    public async Task<ActionResult<TrainerPortalMeDto>> UpdateMe(UpdateTrainerPortalProfileRequest request)
    {
        return await HandleAsync<TrainerPortalMeDto>(async () =>
        {
            var result = await _trainerPortalService.UpdateMeAsync(request);
            return result is null ? NotFound() : Ok(result);
        });
    }

    [HttpPost("me/avatar")]
    [Consumes("multipart/form-data")]
    public async Task<ActionResult<AvatarDto>> UploadMyAvatar(
        IFormFile file,
        CancellationToken cancellationToken)
    {
        if (file is null)
            return BadRequest(new { message = "Avatar file is required." });

        return await HandleAsync<AvatarDto>(async () =>
        {
            await using var stream = file.OpenReadStream();
            return Ok(await _avatarService.UploadCurrentUserAvatarAsync(
                stream,
                file.FileName,
                file.ContentType,
                file.Length,
                cancellationToken));
        });
    }

    [HttpDelete("me/avatar")]
    public async Task<ActionResult<AvatarDto>> DeleteMyAvatar(CancellationToken cancellationToken)
    {
        return await HandleAsync<AvatarDto>(async () =>
            Ok(await _avatarService.DeleteCurrentUserAvatarAsync(cancellationToken)));
    }

    [HttpGet("clients")]
    public async Task<ActionResult<List<TrainerPortalClientDto>>> GetClients()
    {
        return Ok(await _trainerPortalService.GetClientsAsync());
    }

    [HttpGet("clients/{clientId:int}")]
    public async Task<ActionResult<ClientDto>> GetClient(int clientId)
    {
        var result = await _trainerPortalService.GetClientAsync(clientId);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpGet("clients/{clientId:int}/workspace")]
    public async Task<ActionResult<ClientWorkspaceDto>> GetClientWorkspace(int clientId)
    {
        return await HandleAsync<ClientWorkspaceDto>(async () =>
        {
            var result = await _trainerPortalService.GetClientWorkspaceAsync(clientId);
            return result is null ? NotFound() : Ok(result);
        });
    }

    [HttpPatch("clients/{clientId:int}")]
    public async Task<ActionResult<ClientDto>> UpdateClient(int clientId, UpdateClientDto request)
    {
        return await HandleAsync<ClientDto>(async () =>
        {
            var result = await _trainerPortalService.UpdateClientAsync(clientId, request);
            return result is null ? NotFound() : Ok(result);
        });
    }

    [HttpPost("clients/{clientId:int}/avatar")]
    [Consumes("multipart/form-data")]
    public async Task<ActionResult<AvatarDto>> UploadClientAvatar(
        int clientId,
        IFormFile file,
        CancellationToken cancellationToken)
    {
        if (file is null)
            return BadRequest(new { message = "Avatar file is required." });

        return await HandleAsync<AvatarDto>(async () =>
        {
            await using var stream = file.OpenReadStream();
            return Ok(await _avatarService.UploadClientAvatarAsync(
                clientId,
                stream,
                file.FileName,
                file.ContentType,
                file.Length,
                cancellationToken));
        });
    }

    [HttpDelete("clients/{clientId:int}/avatar")]
    public async Task<ActionResult<AvatarDto>> DeleteClientAvatar(
        int clientId,
        CancellationToken cancellationToken)
    {
        return await HandleAsync<AvatarDto>(async () =>
            Ok(await _avatarService.DeleteClientAvatarAsync(clientId, cancellationToken)));
    }

    [HttpPost("clients/{clientId:int}/deactivate")]
    public async Task<IActionResult> DeactivateClient(int clientId)
    {
        var result = await _trainerPortalService.DeactivateClientAsync(clientId);
        return result ? NoContent() : NotFound();
    }

    [HttpGet("invitations")]
    public async Task<ActionResult<List<InvitationDto>>> GetInvitations([FromQuery] InvitationFilterDto filter)
    {
        try
        {
            filter.Role = "Client";
            return Ok(await _invitationService.GetAllAsync(filter));
        }
        catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.UndefinedColumn)
        {
            return Conflict(new { message = "Invitation list could not be loaded because the database schema is not up to date. Wait for the latest deploy to finish and try again." });
        }
    }

    [HttpGet("invitations/{id:int}")]
    public async Task<ActionResult<InvitationDto>> GetInvitation(int id)
    {
        try
        {
            var result = await _invitationService.GetByIdAsync(id);
            return result is null ? NotFound() : Ok(result);
        }
        catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.UndefinedColumn)
        {
            return Conflict(new { message = "Invitation could not be loaded because the database schema is not up to date. Wait for the latest deploy to finish and try again." });
        }
    }

    [HttpPost("invitations")]
    public async Task<ActionResult<InvitationDto>> CreateInvitation(CreateInvitationDto request)
    {
        return await HandleAsync<InvitationDto>(async () =>
        {
            request.Role = "Client";
            var result = await _invitationService.CreateAsync(request);
            return CreatedAtAction(nameof(GetInvitation), new { id = result.Id }, result);
        });
    }

    [HttpPost("invitations/{id:int}/resend")]
    public async Task<ActionResult<InvitationDto>> ResendInvitation(int id)
    {
        return await HandleAsync<InvitationDto>(async () =>
        {
            var result = await _invitationService.ResendAsync(id);
            return result is null ? NotFound() : Ok(result);
        });
    }

    [HttpPost("invitations/{id:int}/cancel")]
    public async Task<IActionResult> CancelInvitation(int id)
    {
        try
        {
            var result = await _invitationService.CancelAsync(id);
            return result ? NoContent() : NotFound();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpDelete("invitations/{id:int}")]
    public Task<IActionResult> DeleteInvitation(int id)
    {
        return CancelInvitation(id);
    }

    [HttpGet("clients/{clientId:int}/billing")]
    public async Task<ActionResult<ClientBillingSummaryDto>> GetClientBilling(int clientId)
    {
        return await HandleAsync<ClientBillingSummaryDto>(async () =>
            Ok(await _clientPaymentService.GetClientSummaryAsync(clientId)));
    }

    [HttpGet("clients/{clientId:int}/subscription")]
    public async Task<ActionResult<SubscriptionDto>> GetClientSubscription(int clientId)
    {
        return await HandleAsync<SubscriptionDto>(async () =>
            Ok(await _subscriptionService.GetClientSubscriptionAsync(clientId)));
    }

    [HttpPut("clients/{clientId:int}/subscription/next-package")]
    public async Task<ActionResult<SubscriptionDto>> SetClientNextPackage(
        int clientId,
        SetNextPackageRequest request)
    {
        return await HandleAsync<SubscriptionDto>(async () =>
            Ok(await _subscriptionService.SetNextPackageAsync(clientId, request)));
    }

    [HttpPost("clients/{clientId:int}/subscription/cancel")]
    public async Task<ActionResult<SubscriptionDto>> CancelClientRenewal(int clientId)
    {
        return await HandleAsync<SubscriptionDto>(async () =>
            Ok(await _subscriptionService.CancelRenewalAsync(clientId)));
    }

    [HttpPost("clients/{clientId:int}/subscription/resume")]
    public async Task<ActionResult<SubscriptionDto>> ResumeClientRenewal(int clientId)
    {
        return await HandleAsync<SubscriptionDto>(async () =>
            Ok(await _subscriptionService.ResumeRenewalAsync(clientId)));
    }

    [HttpGet("clients/{clientId:int}/subscription/current-cycle/usage")]
    public async Task<ActionResult<SubscriptionUsageDto>> GetClientSubscriptionUsage(int clientId)
    {
        return await HandleAsync<SubscriptionUsageDto>(async () =>
            Ok(await _subscriptionService.GetClientUsageAsync(clientId)));
    }

    [HttpGet("clients/{clientId:int}/training-plan")]
    public async Task<ActionResult<TrainingPlanDto>> GetClientTrainingPlan(int clientId)
    {
        return await HandleAsync<TrainingPlanDto>(async () =>
            Ok(await _subscriptionService.GetClientTrainingPlanAsync(clientId)));
    }

    [HttpPut("clients/{clientId:int}/training-plan")]
    public async Task<ActionResult<TrainingPlanDto>> UpdateClientTrainingPlan(
        int clientId,
        UpdateTrainingPlanRequest request)
    {
        return await HandleAsync<TrainingPlanDto>(async () =>
            Ok(await _subscriptionService.UpdateTrainingPlanAsync(clientId, request)));
    }

    [HttpPost("clients/{clientId:int}/packages")]
    public async Task<ActionResult<object>> CreateClientPackage(
        int clientId,
        CreateClientPackageRequest request)
    {
        return await HandleAsync<object>(async () =>
        {
            request.ClientId = clientId;
            var id = await _clientPackageService.CreateAsync(request);
            return CreatedAtAction(nameof(GetClientBilling), new { clientId }, new { id });
        });
    }

    [HttpPost("clients/{clientId:int}/packages/{clientPackageId:int}/activate")]
    public async Task<IActionResult> ActivateClientPackage(int clientId, int clientPackageId)
    {
        try
        {
            var activated = await _clientPackageService.ActivateAsync(clientId, clientPackageId);
            return activated ? NoContent() : NotFound();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("clients/{clientId:int}/payments")]
    public async Task<ActionResult<ClientPaymentDto>> CreateClientPayment(
        int clientId,
        CreateClientPaymentRequest request)
    {
        return await HandleAsync<ClientPaymentDto>(async () =>
        {
            request.ClientId = clientId;
            var payment = await _clientPaymentService.CreatePaymentAsStaffAsync(request);
            return CreatedAtAction(nameof(GetClientBilling), new { clientId = payment.ClientId }, payment);
        });
    }

    [HttpGet("payments/pending")]
    public async Task<ActionResult<List<ClientPaymentDto>>> GetPendingPayments()
    {
        return await HandleAsync<List<ClientPaymentDto>>(async () =>
            Ok(await _clientPaymentService.GetPendingConfirmationsAsync()));
    }

    [HttpPost("payments/{paymentId:int}/confirm")]
    public async Task<ActionResult<ClientPaymentDto>> ConfirmPayment(int paymentId)
    {
        return await HandleAsync<ClientPaymentDto>(async () =>
            Ok(await _clientPaymentService.ConfirmAsync(paymentId)));
    }

    [HttpPost("payments/{paymentId:int}/reject")]
    public async Task<ActionResult<ClientPaymentDto>> RejectPayment(
        int paymentId,
        RejectClientPaymentRequest request)
    {
        return await HandleAsync<ClientPaymentDto>(async () =>
            Ok(await _clientPaymentService.RejectAsync(paymentId, request)));
    }

    [HttpGet("sessions")]
    public async Task<ActionResult<List<TrainerPortalSessionDto>>> GetSessions()
    {
        return Ok(await _trainerPortalService.GetSessionsAsync());
    }

    [HttpGet("sessions/{sessionId:int}")]
    public async Task<ActionResult<SessionDto>> GetSession(int sessionId)
    {
        var result = await _trainerPortalService.GetSessionAsync(sessionId);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpGet("sessions/{sessionId:int}/workspace")]
    public async Task<ActionResult<SessionWorkspaceDto>> GetSessionWorkspace(int sessionId)
    {
        return await HandleAsync<SessionWorkspaceDto>(async () =>
        {
            var result = await _trainerPortalService.GetSessionWorkspaceAsync(sessionId);
            return result is null ? NotFound() : Ok(result);
        });
    }

    [HttpPost("sessions")]
    public async Task<ActionResult<SessionDto>> CreateSession(CreateSessionDto request)
    {
        return await HandleAsync<SessionDto>(async () =>
        {
            var result = await _trainerPortalService.CreateSessionAsync(request);
            return CreatedAtAction(nameof(GetSession), new { sessionId = result.Id }, result);
        });
    }

    [HttpPut("sessions/{sessionId:int}")]
    public async Task<ActionResult<SessionDto>> UpdateSession(
        int sessionId,
        UpdateSessionDto request)
    {
        return await HandleAsync<SessionDto>(async () =>
        {
            var result = await _trainerPortalService.UpdateSessionAsync(sessionId, request);
            return result is null ? NotFound() : Ok(result);
        });
    }

    [HttpPost("sessions/{sessionId:int}/complete")]
    public async Task<IActionResult> CompleteSession(
        int sessionId,
        CompleteSessionDto request)
    {
        try
        {
            var result = await _sessionParticipantService.CompleteSessionAsync(sessionId, request);
            return result ? Ok(new { message = "Session completed successfully." }) : NotFound();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("sessions/participants/count-from-package")]
    public async Task<IActionResult> CountFromPackage(CountSessionFromPackageRequest request)
    {
        try
        {
            await _sessionService.CountSessionFromPackageAsync(request);
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpGet("dashboard")]
    public async Task<ActionResult<TrainerPortalDashboardDto>> GetDashboard()
    {
        var result = await _trainerPortalService.GetDashboardAsync();
        return result is null ? NotFound() : Ok(result);
    }

    [HttpGet("settlement")]
    public async Task<ActionResult<TrainerMonthlySettlementDto>> GetMySettlement(
        [FromQuery] int year,
        [FromQuery] int month)
    {
        var result = await _trainerPortalService.GetMyMonthlySettlementAsync(year, month);

        if (result is null)
            return NotFound();

        return Ok(result);
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
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException postgresException &&
                                          postgresException.SqlState == PostgresErrorCodes.UndefinedColumn)
        {
            return Conflict(new { message = "Request could not be saved because the database schema is not up to date. Wait for the latest deploy to finish and try again." });
        }
    }
}
