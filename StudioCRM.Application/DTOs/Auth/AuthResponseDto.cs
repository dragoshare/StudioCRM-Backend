namespace StudioCRM.Application.DTOs.Auth;

public class AuthResponseDto
{
    public string Token { get; set; } = string.Empty;
    public string RefreshToken { get; set; } = string.Empty;

    // Null for the platform-only SuperAdmin panel.
    public Guid? OrganizationId { get; set; }

    public int UserId { get; set; }

    public string Email { get; set; } = string.Empty;

    public string Role { get; set; } = string.Empty;

    public bool EmailVerified { get; set; }

    public bool EmailVerificationRequired { get; set; }
}
