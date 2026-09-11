using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StudioCRM.Application.DTOs.Notifications;
using StudioCRM.Application.Interfaces;
using StudioCRM.Application.Common;

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
        [FromQuery] int limit = 50,
        [FromQuery] string? category = null,
        [FromQuery] bool? isRead = null)
    {
        try { return Ok(await _notificationService.GetCurrentUserNotificationsAsync(limit, category, isRead)); }
        catch (ArgumentException ex) { return BadRequest(new { message = ex.Message }); }
    }

    [HttpGet("categories")]
    public ActionResult<IReadOnlyList<NotificationCategoryDto>> GetCategories() => Ok(NotificationCategories.All);

    [HttpGet("unread-count")]
    public async Task<ActionResult<NotificationUnreadCountDto>> GetUnreadCount([FromQuery] string? category = null)
    {
        try { return Ok(await _notificationService.GetUnreadCountAsync(category)); }
        catch (ArgumentException ex) { return BadRequest(new { message = ex.Message }); }
    }

    [HttpPost("{id:int}/read")]
    public async Task<ActionResult<NotificationDto>> MarkAsRead(int id)
    {
        var result = await _notificationService.MarkAsReadAsync(id);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpPost("read-all")]
    public async Task<ActionResult<NotificationReadAllResultDto>> MarkAllAsRead([FromQuery] string? category = null)
    {
        try { return Ok(await _notificationService.MarkAllAsReadAsync(category)); }
        catch (ArgumentException ex) { return BadRequest(new { message = ex.Message }); }
    }
}
