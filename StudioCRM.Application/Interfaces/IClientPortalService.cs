using StudioCRM.Application.DTOs.ClientPortal;
using StudioCRM.Application.DTOs.Profiles;

namespace StudioCRM.Application.Interfaces;

public interface IClientPortalService
{
    Task<ClientPortalMeDto?> GetMeAsync();
    Task<ClientPortalMeDto?> UpdateMeAsync(UpdateClientPortalProfileRequest request);
    Task RequestEmailChangeAsync(RequestEmailChangeDto request);

    Task<ClientPortalDashboardDto?> GetDashboardAsync();

    Task<List<ClientPortalSessionDto>> GetScheduleAsync();

    Task<ClientTrainerContactDto?> GetTrainerContactAsync(int userId);
    Task<ClientOwnerContactDto?> GetOwnerContactAsync();
}
