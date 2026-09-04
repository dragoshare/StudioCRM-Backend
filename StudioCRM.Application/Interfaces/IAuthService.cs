using StudioCRM.Application.DTOs.Auth;

using StudioCRM.Application.DTOs.Public;

namespace StudioCRM.Application.Interfaces;

public interface IAuthService
{
    Task<CreatedAccountDto> RegisterAsync(RegisterDto request);
    Task<AuthResponseDto> RegisterPublicGroupClientAsync(PublicGroupRegisterRequest request);
    Task<AuthResponseDto?> LoginAsync(LoginRequestDto request);
    Task<AuthMeDto?> GetMeAsync(int userId);
    Task<AuthResponseDto> RefreshAsync(RefreshTokenRequestDto request);
    Task ForgotPasswordAsync(ForgotPasswordDto request);
    Task ResetPasswordAsync(ResetPasswordDto request);
    Task ChangePasswordAsync(int userId, ChangePasswordDto request);
}
