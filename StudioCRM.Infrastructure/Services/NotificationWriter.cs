using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using StudioCRM.Domain.Entities;
using StudioCRM.Infrastructure.Persistence;

namespace StudioCRM.Infrastructure.Services;

internal static class NotificationWriter
{
    public static async Task QueueAsync(
        StudioCRMDbContext context,
        IEnumerable<int> userIds,
        string sourceKey,
        string type,
        string title,
        string? message = null,
        string severity = "Info",
        string? relatedEntityType = null,
        int? relatedEntityId = null,
        string? actionUrl = null,
        DateTime? createdAt = null)
    {
        var recipients = userIds.Distinct().ToList();
        if (recipients.Count == 0)
            return;

        var normalizedSourceKey = NormalizeSourceKey(sourceKey);
        var trackedRecipients = context.Notifications.Local
            .Where(x => x.SourceKey == normalizedSourceKey && recipients.Contains(x.UserId))
            .Select(x => x.UserId)
            .ToHashSet();
        var persistedRecipients = await context.Notifications
            .Where(x => x.SourceKey == normalizedSourceKey && recipients.Contains(x.UserId))
            .Select(x => x.UserId)
            .ToListAsync();

        trackedRecipients.UnionWith(persistedRecipients);

        foreach (var userId in recipients.Where(x => !trackedRecipients.Contains(x)))
        {
            await context.Notifications.AddAsync(new Notification
            {
                UserId = userId,
                SourceKey = normalizedSourceKey,
                Type = type,
                Severity = severity,
                Title = title,
                Message = message,
                RelatedEntityType = relatedEntityType,
                RelatedEntityId = relatedEntityId,
                ActionUrl = actionUrl,
                CreatedAt = createdAt ?? DateTime.UtcNow
            });
        }
    }

    public static async Task QueueForOwnersAsync(
        StudioCRMDbContext context,
        string sourceKey,
        string type,
        string title,
        string? message = null,
        string severity = "Info",
        string? relatedEntityType = null,
        int? relatedEntityId = null,
        string? actionUrl = null,
        DateTime? createdAt = null)
    {
        var ownerIds = await context.Users
            .Where(x => x.IsActive && x.UserRoles.Any(ur => ur.Role.Name == "Owner"))
            .Select(x => x.Id)
            .ToListAsync();

        await QueueAsync(
            context,
            ownerIds,
            sourceKey,
            type,
            title,
            message,
            severity,
            relatedEntityType,
            relatedEntityId,
            actionUrl,
            createdAt);
    }

    private static string NormalizeSourceKey(string sourceKey)
    {
        if (sourceKey.Length <= 300)
            return sourceKey;

        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(sourceKey)))
            .ToLowerInvariant();
        return $"event:{hash}";
    }
}
