using StudioCRM.Application.DTOs.ClientPortal;
using StudioCRM.Application.DTOs.Clients;

namespace StudioCRM.Application.Interfaces;

public interface IClientService
{
    Task<ClientDto> CreateAsync(CreateClientDto request);
    Task<List<ClientDto>> GetAllAsync();
    Task<ClientDto?> GetByIdAsync(int id);
    Task<ClientDto?> UpdateAsync(int id, UpdateClientDto request);
    Task<bool> DeleteAsync(int id);
    Task<bool> AssignTrainerAsync(int id, SetClientTrainerRequest request);
    Task<List<ClientDto>> GetFilteredAsync(ClientFilterDto filter);
    Task<bool> RestoreAsync(int id);
    Task<List<ClientDto>> GetDeletedAsync();
}
