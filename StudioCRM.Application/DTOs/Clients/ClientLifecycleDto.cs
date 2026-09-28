namespace StudioCRM.Application.DTOs.Clients;

public class ClientArchiveCheckDto
{
    public int ClientId { get; set; }
    public bool CanArchive => Blockers.Count == 0;
    public List<string> Blockers { get; set; } = new();
}

public class BulkArchiveClientsRequest
{
    public List<int> ClientIds { get; set; } = new();
}

public class ClientArchiveResultDto
{
    public int ClientId { get; set; }
    public bool Archived { get; set; }
    public string? Error { get; set; }
}

public class SetClientPortalAccessRequest
{
    public bool Blocked { get; set; }
}
