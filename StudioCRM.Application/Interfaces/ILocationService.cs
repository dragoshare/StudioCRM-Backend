using StudioCRM.Application.DTOs.Locations;

namespace StudioCRM.Application.Interfaces;

public interface ILocationService
{
    Task<List<LocationDto>> GetAllAsync();
    Task<LocationDto?> GetByIdAsync(int id);
    Task<LocationDto> CreateAsync(CreateLocationDto request);
}