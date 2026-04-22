using StudioCRM.Application.DTOs.Invitations;

namespace StudioCRM.Application.Interfaces;

public interface IInvitationService
{
    Task<InvitationDto> CreateAsync(CreateInvitationDto request);
    Task<List<InvitationDto>> GetAllAsync(InvitationFilterDto? filter = null);
    Task<InvitationDto?> GetByIdAsync(int id);
    Task<ValidateInvitationDto?> ValidateAsync(string token);
    Task<bool> AcceptAsync(AcceptInvitationDto request);
    Task<InvitationDto?> ResendAsync(int id);
    Task<bool> CancelAsync(int id);
}