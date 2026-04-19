using StudioCRM.Application.DTOs.Sessions;

namespace StudioCRM.Application.Interfaces;

public interface ISessionService
{
    Task<SessionDto> CreateAsync(CreateSessionDto request);
    Task<List<SessionDto>> GetAllAsync();
    Task<SessionDto?> GetByIdAsync(int id);
}