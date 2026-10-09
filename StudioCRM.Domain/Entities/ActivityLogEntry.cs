namespace StudioCRM.Domain.Entities;

// No foreign keys: history survives deletion of the actor and the audited object.
public class ActivityLogEntry
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
    public string? BeforeJson { get; set; }
    public string? AfterJson { get; set; }
    public string ChangedFieldsJson { get; set; } = "[]";
}
