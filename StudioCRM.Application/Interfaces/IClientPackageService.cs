using StudioCRM.Application.DTOs.ClientPackages;
namespace StudioCRM.Application.Interfaces;

public interface IClientPackageService
{
    Task<int> CreateAsync(CreateClientPackageRequest request);
}