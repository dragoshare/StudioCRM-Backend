using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using StudioCRM.Application.DTOs.ActivityLog;
using StudioCRM.Application.DTOs.Billing;
using StudioCRM.Application.Interfaces;
using StudioCRM.Infrastructure.Persistence;

namespace StudioCRM.Infrastructure.Services;

public class ActivityLogService(StudioCRMDbContext db, ICurrentUserService currentUser) : IActivityLogService
{
    private static readonly string[] Operations = ["Created", "Updated", "Deleted", "Archived", "Restored"];
    private static readonly string[] Sources = ["User", "System"];
    private void EnsureOwner()
    {
        if (!currentUser.IsAuthenticated || !currentUser.IsOwner)
            throw new UnauthorizedAccessException("Only owner can read activity history.");
    }

    public ActivityLogMetadataDto GetMetadata()
    {
        EnsureOwner();
        return new() { EntityTypes = ActivityLogCapture.Fields.Keys.Order().ToArray(), Operations = Operations.ToArray(), Sources = Sources.ToArray() };
    }

    public async Task<PagedResultDto<ActivityLogDto>> GetAsync(ActivityLogFilter filter, CancellationToken cancellationToken = default)
    {
        EnsureOwner();
        if (!Validator.TryValidateObject(filter, new ValidationContext(filter), new List<ValidationResult>(), true))
            throw new InvalidOperationException("Invalid activity log filter.");
        if (filter.From.HasValue && filter.To.HasValue && filter.From >= filter.To)
            throw new InvalidOperationException("From must be earlier than To.");
        if (filter.Operation != null && !Operations.Contains(filter.Operation) ||
            filter.Source != null && !Sources.Contains(filter.Source) ||
            filter.EntityType != null && !ActivityLogCapture.Fields.ContainsKey(filter.EntityType))
            throw new InvalidOperationException("Unknown operation, source or entity type.");
        if (filter.EntityId != null && filter.EntityType == null)
            throw new InvalidOperationException("EntityId requires EntityType.");

        var query = db.ActivityLogEntries.AsNoTracking();
        if (filter.From.HasValue) query = query.Where(e => e.CreatedAt >= filter.From.Value.UtcDateTime);
        if (filter.To.HasValue) query = query.Where(e => e.CreatedAt < filter.To.Value.UtcDateTime);
        if (filter.ActorUserId.HasValue) query = query.Where(e => e.ActorUserId == filter.ActorUserId);
        if (filter.Source != null) query = query.Where(e => e.Source == filter.Source);
        if (filter.Operation != null) query = query.Where(e => e.Operation == filter.Operation);
        if (filter.EntityType != null) query = query.Where(e => e.EntityType == filter.EntityType);
        if (filter.EntityId != null) query = query.Where(e => e.EntityId == filter.EntityId);
        if (filter.ChangeSetId.HasValue) query = query.Where(e => e.ChangeSetId == filter.ChangeSetId);
        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var term = filter.Search.Trim().ToLowerInvariant();
            query = query.Where(e => e.ActorName.ToLower().Contains(term) || e.EntityLabel.ToLower().Contains(term));
        }
        var total = await query.CountAsync(cancellationToken);
        var entries = await query.OrderByDescending(e => e.CreatedAt).ThenByDescending(e => e.Id)
            .Skip((filter.Page - 1) * filter.PageSize).Take(filter.PageSize).ToListAsync(cancellationToken);
        return new()
        {
            Page = filter.Page, PageSize = filter.PageSize, TotalCount = total,
            TotalPages = (int)Math.Ceiling(total / (double)filter.PageSize),
            Items = entries.Select(e => new ActivityLogDto
            {
                Id = e.Id, ChangeSetId = e.ChangeSetId, CreatedAt = e.CreatedAt,
                ActorUserId = e.ActorUserId, ActorName = e.ActorName, Source = e.Source,
                Operation = e.Operation, EntityType = e.EntityType, EntityId = e.EntityId, EntityLabel = e.EntityLabel,
                Before = e.BeforeJson == null ? null : JsonSerializer.Deserialize<JsonElement>(e.BeforeJson),
                After = e.AfterJson == null ? null : JsonSerializer.Deserialize<JsonElement>(e.AfterJson),
                ChangedFields = JsonSerializer.Deserialize<List<string>>(e.ChangedFieldsJson)!
            }).ToList()
        };
    }
}
