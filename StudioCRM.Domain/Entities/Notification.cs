namespace StudioCRM.Domain.Entities;

public class Notification
{
    public int Id { get; set; }

    public int UserId { get; set; }

    public string Type { get; set; } = string.Empty;

    public string SourceKey { get; set; } = string.Empty;

    public string Severity { get; set; } = "Info";

    public string Title { get; set; } = string.Empty;

    public string? Message { get; set; }

    public bool IsRead { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? ReadAt { get; set; }

    public string? RelatedEntityType { get; set; }

    public int? RelatedEntityId { get; set; }

    public string? ActionUrl { get; set; }

    public User User { get; set; } = null!;
}
