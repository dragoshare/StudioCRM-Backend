namespace StudioCRM.Domain.Entities;

public class CalendarIntegration
{
    public int Id { get; set; }

    public int UserId { get; set; }

    public string Provider { get; set; } = "Outlook";

    public string ExternalUserId { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public string AccessToken { get; set; } = string.Empty;

    public string RefreshToken { get; set; } = string.Empty;

    public DateTime AccessTokenExpiresAt { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime ConnectedAt { get; set; } = DateTime.UtcNow;

    public DateTime? DisconnectedAt { get; set; }

    public User User { get; set; } = null!;
}