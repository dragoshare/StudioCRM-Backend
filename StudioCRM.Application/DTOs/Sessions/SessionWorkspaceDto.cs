namespace StudioCRM.Application.DTOs.Sessions;

public class SessionWorkspaceDto
{
    public SessionDto Session { get; set; } = new();
    public SessionOutlookSyncDto OutlookSync { get; set; } = new();
    public SessionWorkspaceQuickActionsDto QuickActions { get; set; } = new();
}

public class SessionOutlookSyncDto
{
    public bool IsSynced { get; set; }
    public string? Provider { get; set; }
    public string? ExternalEventId { get; set; }
    public DateTime? LastSyncedAt { get; set; }
    public List<string> Warnings { get; set; } = new();
}

public class SessionWorkspaceQuickActionsDto
{
    public bool CanEditParticipants { get; set; }
    public bool CanComplete { get; set; }
    public bool CanSyncOutlook { get; set; } = true;
}
