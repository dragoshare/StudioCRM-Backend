using StudioCRM.Application.DTOs.Notifications;

namespace StudioCRM.Application.Interfaces;

public interface INotificationService
{
    Task<NotificationDto?> GetByIdAsync(int id);
    Task<StudioCRM.Application.DTOs.Billing.PagedResultDto<NotificationDto>> GetPageAsync(int page, int pageSize, string? category, bool? isRead);
    Task<List<NotificationDto>> GetCurrentUserNotificationsAsync(int limit, string? category = null, bool? isRead = null);
    Task<NotificationUnreadCountDto> GetUnreadCountAsync(string? category = null);
    Task<NotificationDto?> MarkAsReadAsync(int id);
    Task<NotificationReadAllResultDto> MarkAllAsReadAsync(string? category = null);
}
