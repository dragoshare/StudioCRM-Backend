namespace StudioCRM.Domain.Entities;
// A purchase intention, never a seat reservation. One intention per client/class.
public class GroupEnrollmentRequest
{
    public int Id { get; set; }
    public int SessionId { get; set; }
    public Session Session { get; set; } = null!;
    public int ClientId { get; set; }
    public Client Client { get; set; } = null!;
    public int ClientPackageId { get; set; }
    public ClientPackage ClientPackage { get; set; } = null!;
    public int? CreatedByUserId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
