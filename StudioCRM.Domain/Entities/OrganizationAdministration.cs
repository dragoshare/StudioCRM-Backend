namespace StudioCRM.Domain.Entities;

// Registry membership is deliberately not a source of CRM authorization until tenant isolation ships.
public class OrganizationMembership
{
    public Guid OrganizationId { get; set; }
    public int UserId { get; set; }
    public bool IsOwner { get; set; }
    public bool IsTrainer { get; set; }
    public DateTime CreatedAt { get; set; }
    public User User { get; set; } = null!;
}

public class PlatformAuditEntry
{
    public long Id { get; set; }
    public Guid OrganizationId { get; set; }
    public int ActorUserId { get; set; }
    public int? SubjectUserId { get; set; }
    public string Action { get; set; } = "";
    public string Reason { get; set; } = "";
    public string BeforeJson { get; set; } = "{}";
    public string AfterJson { get; set; } = "{}";
    public DateTime CreatedAt { get; set; }
}
