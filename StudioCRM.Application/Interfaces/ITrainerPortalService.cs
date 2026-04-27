using StudioCRM.Application.DTOs.TrainerPortal;
using StudioCRM.Application.DTOs.TrainerSettlements;

namespace StudioCRM.Application.Interfaces;

public interface ITrainerPortalService
{
    Task<TrainerPortalMeDto?> GetMeAsync();
    Task<List<TrainerPortalClientDto>> GetClientsAsync();
    Task<List<TrainerPortalSessionDto>> GetSessionsAsync();
    Task<TrainerPortalDashboardDto?> GetDashboardAsync();
    Task<TrainerMonthlySettlementDto?> GetMyMonthlySettlementAsync(int year, int month);
}