using StudioCRM.Application.DTOs.Notifications;

namespace StudioCRM.Application.Interfaces;

public interface INotificationService
{
    Task<List<NotificationDto>> GetCurrentUserNotificationsAsync(int limit);
    Task<NotificationUnreadCountDto> GetUnreadCountAsync();
    Task<NotificationDto?> MarkAsReadAsync(int id);
    Task<NotificationReadAllResultDto> MarkAllAsReadAsync();
}
