using Microsoft.Extensions.Options;
using Resend;
using StudioCRM.Application.Interfaces;
using StudioCRM.Application.Settings;

namespace StudioCRM.Infrastructure.Services;

public class EmailService : IEmailService
{
    private readonly IResend _resend;
    private readonly EmailSettings _emailSettings;

    public EmailService(
        IResend resend,
        IOptions<EmailSettings> emailOptions)
    {
        _resend = resend;
        _emailSettings = emailOptions.Value;
    }

    public async Task SendInvitationEmailAsync(
        string toEmail,
        string role,
        string locationName,
        string inviteLink)
    {
        var html = $"""
        <div style="font-family: Arial, sans-serif; max-width: 600px; margin: 0 auto; color: #111827;">
            <h2>Zaproszenie do StudioCRM</h2>

            <p>Zostałeś zaproszony do systemu StudioCRM.</p>

            <p>
                Rola: <strong>{role}</strong><br />
                Lokalizacja: <strong>{locationName}</strong>
            </p>

            <p>Kliknij poniżej, aby dokończyć rejestrację:</p>

            <p>
                <a href="{inviteLink}"
                   style="display:inline-block; padding:12px 20px; background:#2563eb; color:white; text-decoration:none; border-radius:8px;">
                    Akceptuj zaproszenie
                </a>
            </p>

            <p>Jeśli przycisk nie działa, skopiuj ten link:</p>
            <p style="word-break: break-all;">{inviteLink}</p>

            <p style="font-size: 12px; color: #6b7280;">
                Jeśli nie spodziewałeś się tej wiadomości, możesz ją zignorować.
            </p>
        </div>
        """;

        var message = new EmailMessage
        {
            From = _emailSettings.From,
            Subject = "Zaproszenie do StudioCRM",
            HtmlBody = html
        };

        message.To.Add(toEmail);

        var response = await _resend.EmailSendAsync(message);

        if (!response.Success)
        {
            throw new InvalidOperationException("Email send failed.");
        }
    }
}