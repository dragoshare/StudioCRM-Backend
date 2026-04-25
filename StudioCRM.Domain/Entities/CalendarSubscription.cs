namespace StudioCRM.Domain.Entities;

public class CalendarSubscription
{
    public int Id { get; set; }

    public int CalendarIntegrationId { get; set; }

    public string Provider { get; set; } = "Outlook";

    public string SubscriptionId { get; set; } = string.Empty;

    public string Resource { get; set; } = string.Empty;

    public DateTime ExpiresAt { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public CalendarIntegration CalendarIntegration { get; set; } = null!;
}