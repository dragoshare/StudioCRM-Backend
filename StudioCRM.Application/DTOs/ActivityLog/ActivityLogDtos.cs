using System.ComponentModel.DataAnnotations;
using System.Text.Json;

namespace StudioCRM.Application.DTOs.ActivityLog;

public class ActivityLogFilter
{
    [Range(1, 100000)] public int Page { get; set; } = 1;
    [Range(1, 100)] public int PageSize { get; set; } = 25;
    public DateTimeOffset? From { get; set; }
    public DateTimeOffset? To { get; set; }
    [Range(1, int.MaxValue)] public int? ActorUserId { get; set; }
    [MaxLength(20)] public string? Source { get; set; }
    [MaxLength(30)] public string? Operation { get; set; }
    [MaxLength(80)] public string? EntityType { get; set; }
    [MaxLength(200)] public string? EntityId { get; set; }
    public Guid? ChangeSetId { get; set; }
    [MaxLength(200)] public string? Search { get; set; }
}

public class ActivityLogDto
{
    public long Id { get; set; }
    public Guid ChangeSetId { get; set; }
    public DateTime CreatedAt { get; set; }
    public int? ActorUserId { get; set; }
    public string ActorName { get; set; } = "";
    public string Source { get; set; } = "";
    public string Operation { get; set; } = "";
    public string EntityType { get; set; } = "";
    public string EntityId { get; set; } = "";
    public string EntityLabel { get; set; } = "";
    public JsonElement? Before { get; set; }
    public JsonElement? After { get; set; }
    public List<string> ChangedFields { get; set; } = new();
}

public class ActivityLogMetadataDto
{
    public string[] EntityTypes { get; set; } = [];
    public string[] Operations { get; set; } = [];
    public string[] Sources { get; set; } = [];
}
