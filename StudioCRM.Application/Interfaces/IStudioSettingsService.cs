using StudioCRM.Application.DTOs.Settings;

namespace StudioCRM.Application.Interfaces;

public interface IStudioSettingsService
{
    Task<OwnerSettingsDto> GetOwnerSettingsAsync();

    Task<OwnerSettingsDto> UpdateOwnerSettingsAsync(UpdateOwnerSettingsDto request);
}
