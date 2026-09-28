namespace StudioCRM.Domain.Entities;

public class ClientAuditEntry
{
    public long Id { get; set; }
    public int ClientId { get; set; }
    public Client Client { get; set; } = null!;
    public int? ActorUserId { get; set; }
    public string Action { get; set; } = string.Empty;
    public string BeforeJson { get; set; } = "{}";
    public string AfterJson { get; set; } = "{}";
    public string? Reason { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
