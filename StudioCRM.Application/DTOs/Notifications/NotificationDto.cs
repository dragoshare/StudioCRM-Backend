namespace StudioCRM.Application.DTOs.Notifications;

public class NotificationDto
{
    public int Id { get; set; }

    public int UserId { get; set; }

    public string Type { get; set; } = string.Empty;

    public string Severity { get; set; } = "Info";

    public string Title { get; set; } = string.Empty;

    public string? Message { get; set; }

    public bool IsRead { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? ReadAt { get; set; }

    public string? RelatedEntityType { get; set; }

    public int? RelatedEntityId { get; set; }

    public string? ActionUrl { get; set; }
}
