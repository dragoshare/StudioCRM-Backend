using StudioCRM.Application.DTOs.Clients;
using StudioCRM.Application.DTOs.Packages;

namespace StudioCRM.Application.Interfaces;

public interface IPackageService
{
    Task<PackageDto> CreateAsync(CreatePackageDto request);
    Task<List<PackageDto>> GetAllAsync();
    Task<PackageDto?> GetByIdAsync(int id);
    Task<PackageDto?> UpdateAsync(int id, UpdatePackageDto request);
    Task<bool> DeleteAsync(int id);
    Task<bool> RestoreAsync(int id);
    Task<List<PackageDto>> GetDeletedAsync();
    Task<List<ClientDto>?> GetClientsAsync(int id);
}
