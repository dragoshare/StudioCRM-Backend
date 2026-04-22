using StudioCRM.Application.DTOs.Invitations;

namespace StudioCRM.Application.Interfaces;

public interface IInvitationService
{
    Task<InvitationDto> CreateAsync(CreateInvitationDto request);
    Task<ValidateInvitationDto?> ValidateAsync(string token);
    Task<bool> AcceptAsync(AcceptInvitationDto request);
}