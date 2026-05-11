using StudioCRM.Application.DTOs.Sessions;

namespace StudioCRM.Application.Interfaces;

public interface ISessionService
{
    Task<List<SessionDto>> GetAllAsync();

    Task<List<SessionDto>> GetFilteredAsync(SessionFilterDto filter);

    Task<SessionDto?> GetByIdAsync(int id);

    Task<SessionWorkspaceDto?> GetWorkspaceAsync(int id);

    Task<SessionDto> CreateAsync(CreateSessionDto request);

    Task<SessionDto?> UpdateAsync(int id, UpdateSessionDto request);

    Task<bool> DeleteAsync(int id);

    Task<bool> RestoreAsync(int id);

    Task<List<SessionDto>> GetDeletedAsync();
    Task CountSessionFromPackageAsync(CountSessionFromPackageRequest request);
}
