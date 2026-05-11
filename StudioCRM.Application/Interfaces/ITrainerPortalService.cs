using StudioCRM.Application.DTOs.Clients;
using StudioCRM.Application.DTOs.Profiles;
using StudioCRM.Application.DTOs.Sessions;
using StudioCRM.Application.DTOs.TrainerPortal;
using StudioCRM.Application.DTOs.TrainerSettlements;

namespace StudioCRM.Application.Interfaces;

public interface ITrainerPortalService
{
    Task<TrainerPortalMeDto?> GetMeAsync();
    Task<TrainerPortalMeDto?> UpdateMeAsync(UpdateTrainerPortalProfileRequest request);
    Task<List<TrainerPortalClientDto>> GetClientsAsync();
    Task<ClientDto?> GetClientAsync(int clientId);
    Task<ClientDto?> UpdateClientAsync(int clientId, UpdateClientDto request);
    Task<bool> DeactivateClientAsync(int clientId);
    Task<List<TrainerPortalSessionDto>> GetSessionsAsync();
    Task<SessionDto?> GetSessionAsync(int sessionId);
    Task<SessionDto> CreateSessionAsync(CreateSessionDto request);
    Task<SessionDto?> UpdateSessionAsync(int sessionId, UpdateSessionDto request);
    Task<TrainerPortalDashboardDto?> GetDashboardAsync();
    Task<TrainerMonthlySettlementDto?> GetMyMonthlySettlementAsync(int year, int month);
}
