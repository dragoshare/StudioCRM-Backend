namespace StudioCRM.Application.Interfaces.Mail;

public interface IEmailService
{
    Task SendLoginEmailChangeVerificationAsync(string toEmail, string verificationLink)
        => throw new NotSupportedException("Login email verification is not configured.");
    Task SendInvitationEmailAsync(
        string toEmail,
        string role,
        string locationName,
        string inviteLink);

    Task SendPasswordResetEmailAsync(
        string toEmail,
        string resetLink);

    Task SendEmailVerificationAsync(
        string toEmail,
        string verificationLink);
}
