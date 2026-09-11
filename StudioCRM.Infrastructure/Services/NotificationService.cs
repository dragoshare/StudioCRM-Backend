using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using StudioCRM.Application.Common;
using StudioCRM.Application.DTOs.Alerts;
using StudioCRM.Application.DTOs.Notifications;
using StudioCRM.Application.Interfaces;
using StudioCRM.Domain.Entities;
using StudioCRM.Infrastructure.Persistence;

namespace StudioCRM.Infrastructure.Services;

public class NotificationService : INotificationService
{
    private readonly StudioCRMDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly IOperationalAlertService _operationalAlertService;

    public NotificationService(
        StudioCRMDbContext context,
        ICurrentUserService currentUser,
        IOperationalAlertService operationalAlertService)
    {
        _context = context;
        _currentUser = currentUser;
        _operationalAlertService = operationalAlertService;
    }

    public async Task<List<NotificationDto>> GetCurrentUserNotificationsAsync(int limit, string? category = null, bool? isRead = null)
    {
        var userId = GetRequiredUserId();
        var safeLimit = limit is <= 0 or > 200 ? 50 : limit;
        var query = FilterCategory(_context.Notifications.Where(n => n.UserId == userId), category);
        if (isRead.HasValue)
            query = query.Where(n => n.IsRead == isRead.Value);

        await SyncOperationalNotificationsAsync(userId);

        return await query
            .AsNoTracking()
            .OrderBy(n => n.IsRead)
            .ThenByDescending(n => n.CreatedAt)
            .ThenByDescending(n => n.Id)
            .Take(safeLimit)
            .Select(n => MapToDto(n))
            .ToListAsync();
    }

    public async Task<NotificationUnreadCountDto> GetUnreadCountAsync(string? category = null)
    {
        var userId = GetRequiredUserId();
        var query = FilterCategory(_context.Notifications.Where(n => n.UserId == userId && !n.IsRead), category);

        await SyncOperationalNotificationsAsync(userId);

        var counts = await query.GroupBy(n => n.Type)
            .Select(g => new { Type = g.Key, Count = g.Count() }).ToListAsync();
        var byCategory = NotificationCategories.All.ToDictionary(x => x.Key, _ => 0);
        foreach (var count in counts)
            byCategory[NotificationCategories.Resolve(count.Type)] += count.Count;

        return new NotificationUnreadCountDto
        {
            UnreadCount = byCategory.Values.Sum(),
            UnreadByCategory = byCategory
        };
    }

    public async Task<NotificationDto?> MarkAsReadAsync(int id)
    {
        var userId = GetRequiredUserId();
        var notification = await _context.Notifications
            .FirstOrDefaultAsync(n => n.Id == id && n.UserId == userId);

        if (notification is null)
            return null;

        MarkRead(notification);
        await _context.SaveChangesAsync();

        return MapToDto(notification);
    }

    public async Task<NotificationReadAllResultDto> MarkAllAsReadAsync(string? category = null)
    {
        var userId = GetRequiredUserId();
        var query = FilterCategory(_context.Notifications.Where(n => n.UserId == userId && !n.IsRead), category);

        await SyncOperationalNotificationsAsync(userId);

        var notifications = await query.ToListAsync();

        foreach (var notification in notifications)
            MarkRead(notification);

        await _context.SaveChangesAsync();

        return new NotificationReadAllResultDto
        {
            MarkedAsRead = notifications.Count
        };
    }

    private int GetRequiredUserId()
    {
        return _currentUser.UserId
            ?? throw new UnauthorizedAccessException("User is not authenticated.");
    }

    private static IQueryable<Notification> FilterCategory(IQueryable<Notification> query, string? category)
    {
        var normalized = NotificationCategories.Normalize(category);
        if (normalized is null)
            return query;
        if (normalized == NotificationCategories.Fallback)
        {
            var nonSystemTypes = NotificationCategories.NonSystemTypes();
            return query.Where(n => !nonSystemTypes.Contains(n.Type));
        }
        var types = NotificationCategories.TypesFor(normalized);
        return query.Where(n => types.Contains(n.Type));
    }

    private static void MarkRead(Notification notification)
    {
        if (notification.IsRead)
            return;

        notification.IsRead = true;
        notification.ReadAt = DateTime.UtcNow;
    }

    private static NotificationDto MapToDto(Notification notification)
    {
        return new NotificationDto
        {
            Id = notification.Id,
            UserId = notification.UserId,
            Type = notification.Type,
            Category = NotificationCategories.Resolve(notification.Type),
            Severity = notification.Severity,
            Title = notification.Title,
            Message = notification.Message,
            IsRead = notification.IsRead,
            CreatedAt = notification.CreatedAt,
            ReadAt = notification.ReadAt,
            RelatedEntityType = notification.RelatedEntityType,
            RelatedEntityId = notification.RelatedEntityId,
            ActionUrl = notification.ActionUrl
        };
    }

    private async Task SyncOperationalNotificationsAsync(int userId)
    {
        if (!_currentUser.IsOwner && !_currentUser.IsTrainer)
            return;

        var alerts = (await _operationalAlertService.GetAlertsAsync(new OperationalAlertFilterDto
        {
            Limit = 200
        })).Items;

        var activeKeys = alerts
            .Select(BuildSourceKey)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var existing = await _context.Notifications
            .Where(n => n.UserId == userId && n.SourceKey.StartsWith("operational:"))
            .ToListAsync();

        var existingByKey = existing
            .ToDictionary(n => n.SourceKey, StringComparer.OrdinalIgnoreCase);

        foreach (var alert in alerts)
        {
            var sourceKey = BuildSourceKey(alert);
            var (relatedEntityType, relatedEntityId) = ResolveRelatedEntity(alert);

            if (!existingByKey.TryGetValue(sourceKey, out var notification))
            {
                notification = new Notification
                {
                    UserId = userId,
                    SourceKey = sourceKey,
                    Type = alert.Type,
                    CreatedAt = alert.CreatedAt
                };

                await _context.Notifications.AddAsync(notification);
                existingByKey[sourceKey] = notification;
            }

            notification.Type = alert.Type;
            notification.Severity = alert.Severity;
            notification.Title = alert.Title;
            notification.Message = alert.Message;
            notification.RelatedEntityType = relatedEntityType;
            notification.RelatedEntityId = relatedEntityId;
            notification.ActionUrl = alert.ActionUrl;
        }

        var resolvedUnreadNotifications = existing
            .Where(n => !n.IsRead && !activeKeys.Contains(n.SourceKey))
            .ToList();

        foreach (var notification in resolvedUnreadNotifications)
            MarkRead(notification);

        await _context.SaveChangesAsync();
    }

    private static string BuildSourceKey(OperationalAlertDto alert)
    {
        var (relatedEntityType, relatedEntityId) = ResolveRelatedEntity(alert);
        var relatedKey = relatedEntityId.HasValue
            ? $"{relatedEntityType}:{relatedEntityId.Value}"
            : $"{alert.Year}:{alert.Month}:{alert.LocationId}:{alert.Title}:{alert.Message}";

        var sourceKey = $"operational:{alert.Type}:{relatedKey}";
        if (sourceKey.Length <= 280)
            return sourceKey;

        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(sourceKey)))
            .ToLowerInvariant();

        return $"operational:{alert.Type}:{hash}";
    }

    private static (string? RelatedEntityType, int? RelatedEntityId) ResolveRelatedEntity(
        OperationalAlertDto alert)
    {
        if (alert.PaymentId.HasValue)
            return ("payment", alert.PaymentId.Value);

        if (alert.ClientPackageId.HasValue)
            return ("client_package", alert.ClientPackageId.Value);

        if (alert.SessionId.HasValue)
            return ("session", alert.SessionId.Value);

        if (alert.InvitationId.HasValue)
            return ("invitation", alert.InvitationId.Value);

        if (alert.TrainerContractId.HasValue)
            return ("trainer_contract", alert.TrainerContractId.Value);

        if (alert.SettlementId.HasValue)
            return ("trainer_settlement", alert.SettlementId.Value);

        if (alert.ClientId.HasValue)
            return ("client", alert.ClientId.Value);

        if (alert.TrainerId.HasValue)
            return ("trainer", alert.TrainerId.Value);

        if (alert.LocationId.HasValue)
            return ("location", alert.LocationId.Value);

        return (null, null);
    }
}
