namespace StudioCRM.Application.Interfaces.Mail;

public interface IEmailService
{
    Task SendInvitationEmailAsync(
        string toEmail,
        string role,
        string locationName,
        string inviteLink);

    Task SendPasswordResetEmailAsync(
        string toEmail,
        string resetLink);
}
