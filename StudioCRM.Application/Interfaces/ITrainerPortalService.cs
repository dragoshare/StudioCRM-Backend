using StudioCRM.Application.DTOs.Clients;
using StudioCRM.Application.DTOs.Profiles;
using StudioCRM.Application.DTOs.Sessions;
using StudioCRM.Application.DTOs.TrainerPortal;
using StudioCRM.Application.DTOs.TrainerSettlements;

namespace StudioCRM.Application.Interfaces;

public interface ITrainerPortalService
{
    Task<StudioCRM.Application.DTOs.Billing.PagedResultDto<TrainerPortalClientDto>> GetClientsPageAsync(PortalPageFilter filter);
    Task<StudioCRM.Application.DTOs.Billing.PagedResultDto<GroupClientOptionDto>> GetGroupClientsAsync(PortalPageFilter filter);
    Task<TrainerDashboardSummaryDto?> GetDashboardSummaryAsync();
    Task<TrainerPortalMeDto?> GetMeAsync();
    Task<TrainerPortalMeDto?> UpdateMeAsync(UpdateTrainerPortalProfileRequest request);
    Task<List<TrainerPortalClientDto>> GetClientsAsync();
    Task<TrainerParticipantProfileDto?> GetParticipantProfileAsync(int sessionId, int clientId);
    Task<ClientDto?> GetClientAsync(int clientId);
    Task<ClientWorkspaceDto?> GetClientWorkspaceAsync(int clientId);
    Task<ClientDto?> UpdateClientAsync(int clientId, UpdateClientDto request);
    Task<bool> DeactivateClientAsync(int clientId);
    Task<List<TrainerPortalSessionDto>> GetSessionsAsync(DateTime? from = null, DateTime? to = null);
    Task<SessionDto?> GetSessionAsync(int sessionId);
    Task<SessionWorkspaceDto?> GetSessionWorkspaceAsync(int sessionId);
    Task<SessionDto> CreateSessionAsync(CreateSessionDto request);
    Task<SessionDto?> UpdateSessionAsync(int sessionId, UpdateSessionDto request);
    Task<SessionSeriesDto> CreateSessionSeriesAsync(CreateSessionSeriesDto request);
    Task<SessionSeriesOutlookSyncDto?> SyncSessionSeriesToOutlookAsync(string recurringGroupId, SessionRecurrenceDto? recurrence);
    Task<DeleteSessionSeriesResultDto?> DeleteSessionSeriesAsync(string recurringGroupId);
    Task<TrainerPortalDashboardDto?> GetDashboardAsync();
    Task<TrainerMonthlySettlementDto?> GetMyMonthlySettlementAsync(int year, int month);
}
