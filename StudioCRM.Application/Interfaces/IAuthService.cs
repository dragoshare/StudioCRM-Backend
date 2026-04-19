using StudioCRM.Application.DTOs.Auth;

namespace StudioCRM.Application.Interfaces;

public interface IAuthService
{
    Task<AuthResponseDto?> LoginAsync(LoginRequestDto request);
    Task<AuthResponseDto> RegisterAsync(RegisterDto request);
    Task<AuthResponseDto> RefreshAsync(RefreshTokenRequestDto request);
    Task ForgotPasswordAsync(ForgotPasswordDto request);
    Task ResetPasswordAsync(ResetPasswordDto request);
}