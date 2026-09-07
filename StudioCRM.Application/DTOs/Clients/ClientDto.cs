namespace StudioCRM.Application.DTOs.Clients;

public class ClientDto
{
    public int Id { get; set; }

    public int? TrainerId { get; set; }

    public int? ActivePackageId { get; set; }

    public int? ActiveClientPackageId { get; set; }

    public string? ActiveClientPackageName { get; set; }

    public int ActivePackageTotalSessions { get; set; }

    public int ActivePackageUsedSessions { get; set; }

    public int ActivePackageRemainingSessions { get; set; }

    public string ActivePackagePaymentStatus { get; set; } = string.Empty;

    public decimal CurrentBalance { get; set; }

    public string FirstName { get; set; } = string.Empty;

    public string LastName { get; set; } = string.Empty;

    public string FullName { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public string EmailContactUrl { get; set; } = string.Empty;

    public string? PhoneNumber { get; set; }

    public string? PhoneContactUrl { get; set; }

    public string? AvatarUrl { get; set; }

    public string? Goal { get; set; }

    public string? Notes { get; set; }

    public string BillingStatus { get; set; } = string.Empty;

    public string Source { get; set; } = string.Empty;

    public string PortalAccessMode { get; set; } = string.Empty;

    public string Status { get; set; } = string.Empty;

    public DateTime? NextSessionAt { get; set; }

    public DateTime? TrainingStartDate { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public int? CreatedBy { get; set; }

    public string? TrainerFullName { get; set; }
    public int LocationId { get; set; }
    public string? LocationName { get; set; }
}
