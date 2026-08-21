using StudioCRM.Application.DTOs.ClientPackages;
namespace StudioCRM.Application.Interfaces;

public interface IClientPackageService
{
    Task<int> CreateAsync(CreateClientPackageRequest request);
    Task<bool> ActivateAsync(int clientId, int clientPackageId);
    Task<bool> DeleteAsync(int clientId, int clientPackageId);
}
