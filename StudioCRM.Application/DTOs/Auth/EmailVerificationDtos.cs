namespace StudioCRM.Application.DTOs.Auth;

public class VerifyEmailRequest
{
    public string Token { get; set; } = string.Empty;
}

public class ResendEmailVerificationRequest
{
    public string Email { get; set; } = string.Empty;
}
