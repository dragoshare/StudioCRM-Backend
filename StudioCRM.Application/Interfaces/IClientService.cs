using StudioCRM.Application.DTOs.Clients;

namespace StudioCRM.Application.Interfaces;

public interface IClientService
{
    Task<ClientDto> CreateAsync(CreateClientDto request);
    Task<List<ClientDto>> GetAllAsync();
    Task<ClientDto?> GetByIdAsync(int id);
}