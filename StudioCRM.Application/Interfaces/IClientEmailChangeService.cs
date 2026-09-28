using StudioCRM.Application.DTOs.Clients;

namespace StudioCRM.Application.Interfaces;

public interface IClientEmailChangeService
{
    Task<List<ClientEmailChangeDto>> GetAsync(int? clientId);
    Task ReviewAsync(int id, ReviewClientEmailChangeRequest request);
    Task VerifyAsync(VerifyClientEmailChangeRequest request);
    Task CancelOwnAsync(int id);
}
