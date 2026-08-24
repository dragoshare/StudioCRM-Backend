namespace StudioCRM.Application.DTOs.TrainerPortal;

public class TrainerPortalClientDto
{
    public int ClientId { get; set; }

    public string FullName { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public string EmailContactUrl { get; set; } = string.Empty;

    public string? PhoneNumber { get; set; }

    public string? PhoneContactUrl { get; set; }

    public string? AvatarUrl { get; set; }

    public string? Goal { get; set; }

    public string Status { get; set; } = string.Empty;

    public string BillingStatus { get; set; } = string.Empty;

    public string LocationName { get; set; } = string.Empty;

    public DateTime? TrainingStartDate { get; set; }

    public DateTime CreatedAt { get; set; }
}
