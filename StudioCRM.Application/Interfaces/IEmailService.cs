namespace StudioCRM.Application.Interfaces;

public interface IEmailService
{
    Task SendInvitationEmailAsync(
        string toEmail,
        string role,
        string locationName,
        string inviteLink);
}