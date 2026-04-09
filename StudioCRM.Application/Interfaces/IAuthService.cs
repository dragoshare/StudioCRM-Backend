using StudioCRM.Application.DTOs.Auth;

namespace StudioCRM.Application.Interfaces;

public interface IAuthService
{
    Task<LoginResponseDto?> LoginAsync(LoginRequestDto request);
}