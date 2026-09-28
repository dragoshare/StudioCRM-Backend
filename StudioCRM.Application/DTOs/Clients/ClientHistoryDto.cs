namespace StudioCRM.Application.DTOs.Clients;

public class ClientHistoryFilter
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 25;
    public string Scope { get; set; } = "All";
}

public class ClientAuditDto
{
    public long Id { get; set; }
    public string Action { get; set; } = "";
    public int? ActorUserId { get; set; }
    public string BeforeJson { get; set; } = "{}";
    public string AfterJson { get; set; } = "{}";
    public string? Reason { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class ClientSessionHistoryDto
{
    public int SessionId { get; set; }
    public string Title { get; set; } = "";
    public DateTime StartAt { get; set; }
    public DateTime EndAt { get; set; }
    public string Status { get; set; } = "";
    public int TrainerId { get; set; }
    public string TrainerName { get; set; } = "";
    public int LocationId { get; set; }
    public string LocationName { get; set; } = "";
    public string AttendanceStatus { get; set; } = "";
    public bool IsCountedFromPackage { get; set; }
    public int? ClientPackageId { get; set; }
}
