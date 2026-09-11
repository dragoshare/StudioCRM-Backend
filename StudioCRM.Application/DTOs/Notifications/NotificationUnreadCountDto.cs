namespace StudioCRM.Application.DTOs.Notifications;

public class NotificationUnreadCountDto
{
    public int UnreadCount { get; set; }
    public Dictionary<string, int> UnreadByCategory { get; set; } = new();
}
