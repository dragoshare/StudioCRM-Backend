namespace StudioCRM.Domain.Entities;

public class Client
{
    public int Id { get; set; }
    public int? UserId { get; set; }
    public int? TrainerId { get; set; }

    public int? ActivePackageId { get; set; }
    public int LocationId { get; set; }

    public string FirstName { get; set; } = string.Empty;

    public string LastName { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public string? PhoneNumber { get; set; }

    public string? GoogleDriveFolderId { get; set; }

    public string? TrainingPlanFileId { get; set; }

    public string? TrainingPlanFileName { get; set; }

    public string? TrainingPlanUrl { get; set; }

    public string? Goal { get; set; }

    public string? Notes { get; set; }

    public string BillingStatus { get; set; } = "Pending";

    public string Source { get; set; } = "Invitation";

    public string Status { get; set; } = "New";

    public DateTime? NextSessionAt { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public int? CreatedBy { get; set; }
    public User? User { get; set; }

    public Trainer? Trainer { get; set; }

    public Package? ActivePackage { get; set; }
    public Location Location { get; set; } = null!;

    public ICollection<Session> Sessions { get; set; } = new List<Session>();
    public bool IsDeleted { get; set; } = false;
    public DateTime? DeletedAt { get; set; }
    public DateTime? TrainingStartDate { get; set; }

    public bool SubscriptionAutoRenewEnabled { get; set; } = true;

    public int? NextPackageId { get; set; }

    public DateTime? RenewalCancellationRequestedAt { get; set; }

    public int? RenewalCancellationRequestedByUserId { get; set; }

    public DateTime? RenewalCancelledAt { get; set; }

    public int? RenewalCancelledByUserId { get; set; }

    public ICollection<ClientMilestone> Milestones { get; set; } = new List<ClientMilestone>();
}
