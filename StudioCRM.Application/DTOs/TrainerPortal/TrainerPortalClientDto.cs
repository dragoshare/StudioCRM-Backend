namespace StudioCRM.Application.DTOs.TrainerPortal;

public class TrainerPortalClientDto
{
    public int ClientId { get; set; }

    public string FullName { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public string? PhoneNumber { get; set; }

    public string Status { get; set; } = string.Empty;

    public string BillingStatus { get; set; } = string.Empty;

    public int ProgressPercent { get; set; }

    public string LocationName { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }
}