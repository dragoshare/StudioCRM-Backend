using StudioCRM.Application.DTOs.SessionParticipants;

namespace StudioCRM.Application.Interfaces;

public interface ISessionParticipantService
{
    Task<List<SessionParticipantDto>> GetBySessionIdAsync(int sessionId);

    Task<SessionParticipantDto> AddParticipantAsync(int sessionId, AddSessionParticipantDto request);

    Task<bool> RemoveParticipantAsync(int sessionId, int participantId);

    Task<bool> CompleteSessionAsync(int sessionId, CompleteSessionDto request);

    Task<bool> CompleteSessionAutomaticallyAsync(int sessionId, CompleteSessionDto request);
}
