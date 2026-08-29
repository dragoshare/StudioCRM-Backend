using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StudioCRM.Application.DTOs.Auth;
using StudioCRM.Application.DTOs.Profiles;
using StudioCRM.Application.Interfaces;

namespace StudioCRM.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;
    private readonly IAvatarService _avatarService;

    public AuthController(
        IAuthService authService,
        IAvatarService avatarService)
    {
        _authService = authService;
        _avatarService = avatarService;
    }

    [Authorize(Roles = "Owner")]
    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterDto request)
    {
        try
        {
            var result = await _authService.RegisterAsync(request);
            return CreatedAtAction(nameof(Me), new { id = result.UserId }, result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [AllowAnonymous]
    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequestDto request)
    {
        var result = await _authService.LoginAsync(request);

        if (result is null)
        {
            return Unauthorized(new { message = "Invalid email or password." });
        }

        return Ok(result);
    }

    [AllowAnonymous]
    [HttpPost("refresh")]
    public async Task<IActionResult> Refresh([FromBody] RefreshTokenRequestDto request)
    {
        var result = await _authService.RefreshAsync(request);
        return Ok(result);
    }

    [AllowAnonymous]
    [HttpPost("forgot-password")]
    public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordDto request)
    {
        await _authService.ForgotPasswordAsync(request);
        return Ok(new { message = "If the account exists, a reset token has been generated." });
    }

    [AllowAnonymous]
    [HttpPost("reset-password")]
    public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordDto request)
    {
        await _authService.ResetPasswordAsync(request);
        return Ok(new { message = "Password has been reset." });
    }

    [Authorize]
    [HttpPost("logout")]
    public IActionResult Logout()
    {
        return Ok(new { message = "Logged out successfully." });
    }

    [Authorize]
    [HttpPost("change-password")]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordDto request)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (!int.TryParse(userId, out var parsedUserId))
            return Unauthorized(new { message = "Invalid user token." });

        try
        {
            await _authService.ChangePasswordAsync(parsedUserId, request);
            return Ok(new { message = "Password has been changed." });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [Authorize]
    [HttpGet("me")]
    public async Task<IActionResult> Me()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (!int.TryParse(userId, out var parsedUserId))
        {
            return Unauthorized(new { message = "Invalid user token." });
        }

        var result = await _authService.GetMeAsync(parsedUserId);

        if (result is null)
        {
            return NotFound();
        }

        return Ok(result);
    }

    [Authorize]
    [HttpPost("me/avatar")]
    [Consumes("multipart/form-data")]
    public async Task<ActionResult<AvatarDto>> UploadMyAvatar(
        IFormFile file,
        CancellationToken cancellationToken)
    {
        if (file is null)
            return BadRequest(new { message = "Avatar file is required." });

        return await HandleAvatarAsync(async () =>
        {
            await using var stream = file.OpenReadStream();
            return Ok(await _avatarService.UploadCurrentUserAvatarAsync(
                stream,
                file.FileName,
                file.ContentType,
                file.Length,
                cancellationToken));
        });
    }

    [Authorize]
    [HttpDelete("me/avatar")]
    public async Task<ActionResult<AvatarDto>> DeleteMyAvatar(CancellationToken cancellationToken)
    {
        return await HandleAvatarAsync(async () =>
            Ok(await _avatarService.DeleteCurrentUserAvatarAsync(cancellationToken)));
    }

    private async Task<ActionResult<AvatarDto>> HandleAvatarAsync(Func<Task<ActionResult<AvatarDto>>> action)
    {
        try
        {
            return await action();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }
}
