using StudioCRM.Application.DTOs.ClientPackages;
namespace StudioCRM.Application.ClientPackages.Interfaces;

public interface IClientPackageService
{
    Task<int> CreateAsync(CreateClientPackageRequest request);
}