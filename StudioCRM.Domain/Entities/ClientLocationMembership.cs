namespace StudioCRM.Domain.Entities;

public class ClientLocationMembership
{
    public int Id { get; set; }

    public int ClientId { get; set; }

    public int LocationId { get; set; }

    public bool IsHomeLocation { get; set; }

    public bool GroupAccessEnabled { get; set; }

    public string Source { get; set; } = "OwnerAssignment";

    public DateTime JoinedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public Client Client { get; set; } = null!;

    public Location Location { get; set; } = null!;
}
