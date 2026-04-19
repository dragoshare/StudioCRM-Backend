using StudioCRM.Application.DTOs.Packages;

namespace StudioCRM.Application.Interfaces;

public interface IPackageService
{
    Task<PackageDto> CreateAsync(CreatePackageDto request);
    Task<List<PackageDto>> GetAllAsync();
    Task<PackageDto?> GetByIdAsync(int id);
}