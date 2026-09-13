namespace StudioCRM.Domain.Entities;

public class SessionCorrection
{
    public int Id { get; set; }

    public int? SessionId { get; set; }

    public int OriginalSessionId { get; set; }

    public string ChangeType { get; set; } = string.Empty;

    public string? Reason { get; set; }

    public string BeforeStateJson { get; set; } = string.Empty;

    public string AfterStateJson { get; set; } = string.Empty;

    public int? ChangedByUserId { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Session? Session { get; set; }
}
