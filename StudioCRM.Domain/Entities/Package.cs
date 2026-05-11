using StudioCRM.Domain.Enums;

namespace StudioCRM.Domain.Entities;

public class Package
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public decimal Price { get; set; }

    public string Currency { get; set; } = "PLN";

    public int SessionsLimit { get; set; }

    public int SessionsPerWeek { get; set; } = 1;

    public int DurationDays { get; set; }

    public SessionBillingType BillingType { get; set; } = SessionBillingType.OneToOne;

    public int ParticipantsCount { get; set; } = 1;

    public int? LocationId { get; set; }

    public Location? Location { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public int? CreatedBy { get; set; }

    public ICollection<Client> Clients { get; set; } = new List<Client>();

    public ICollection<Session> Sessions { get; set; } = new List<Session>();

    public bool IsDeleted { get; set; } = false;

    public DateTime? DeletedAt { get; set; }
}
