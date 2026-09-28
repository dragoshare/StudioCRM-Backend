namespace StudioCRM.Application.DTOs.Clients;

public class ClientLocationDto
{
    public int LocationId { get; set; }
    public string Name { get; set; } = "";
    public bool IsHomeLocation { get; set; }
    public bool GroupAccessEnabled { get; set; }
}
public class SetClientLocationAccessRequest
{
    public bool GroupAccessEnabled { get; set; }
    public string Reason { get; set; } = "";
}
public class ClientDuplicateFilter
{
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string? Name { get; set; }
    public int? ExcludeClientId { get; set; }
}
public class ClientDuplicateDto
{
    public int ClientId { get; set; }
    public string FullName { get; set; } = "";
    public bool IsArchived { get; set; }
    public List<string> Matches { get; set; } = new();
}
public class ClientRefundDto
{
    public int ClientId { get; set; }
    public int ClientPackageId { get; set; }
    public string PackageName { get; set; } = "";
    public string Disposition { get; set; } = "";
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "";
    public DateTime? ConfirmedAt { get; set; }
    public string? Reference { get; set; }
}
