using StudioCRM.Application.DTOs.Sessions;

namespace StudioCRM.Application.Interfaces;

public interface ISessionService
{
    Task<SessionDto> CreateAsync(CreateSessionDto request);
    Task<List<SessionDto>> GetAllAsync();
    Task<SessionDto?> GetByIdAsync(int id);
    Task<SessionDto?> UpdateAsync(int id, UpdateSessionDto request);
    Task<bool> DeleteAsync(int id);
    Task<List<SessionDto>> GetFilteredAsync(SessionFilterDto filter);
    Task<bool> RestoreAsync(int id);
    Task<List<SessionDto>> GetDeletedAsync();
}