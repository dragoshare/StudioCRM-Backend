using StudioCRM.Application.DTOs.ClientPortal;

namespace StudioCRM.Application.Interfaces;

public interface IClientPortalService
{
    Task<ClientPortalMeDto?> GetMeAsync();

    Task<ClientPortalDashboardDto?> GetDashboardAsync();

    Task<List<ClientPortalSessionDto>> GetScheduleAsync();

    Task<ClientPortalPackageDto?> GetPackageAsync();

    Task<ClientPortalPaymentDto?> GetPaymentAsync();

    Task<ClientPortalTrainerDto?> GetTrainerAsync();
}