using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StudioCRM.Application.DTOs.Notifications;
using StudioCRM.Application.Interfaces;

namespace StudioCRM.Api.Controllers;

[ApiController]
[Route("api/Notifications")]
[Authorize]
public class NotificationsController : ControllerBase
{
    private readonly INotificationService _notificationService;

    public NotificationsController(INotificationService notificationService)
    {
        _notificationService = notificationService;
    }

    [HttpGet]
    public async Task<ActionResult<List<NotificationDto>>> GetNotifications(
        [FromQuery] int limit = 50)
    {
        return Ok(await _notificationService.GetCurrentUserNotificationsAsync(limit));
    }

    [HttpGet("unread-count")]
    public async Task<ActionResult<NotificationUnreadCountDto>> GetUnreadCount()
    {
        return Ok(await _notificationService.GetUnreadCountAsync());
    }

    [HttpPost("{id:int}/read")]
    public async Task<ActionResult<NotificationDto>> MarkAsRead(int id)
    {
        var result = await _notificationService.MarkAsReadAsync(id);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpPost("read-all")]
    public async Task<ActionResult<NotificationReadAllResultDto>> MarkAllAsRead()
    {
        return Ok(await _notificationService.MarkAllAsReadAsync());
    }
}
