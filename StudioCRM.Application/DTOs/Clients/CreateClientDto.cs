namespace StudioCRM.Application.DTOs.Clients;

public class CreateClientDto
{
    public int? TrainerId { get; set; }

    public int? ActivePackageId { get; set; }

    public string FirstName { get; set; } = string.Empty;

    public string LastName { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public string? PhoneNumber { get; set; }

    public string? AvatarUrl { get; set; }

    public string? Goal { get; set; }

    public string? Notes { get; set; }

    public int ProgressPercent { get; set; } = 0;

    public string? BillingStatus { get; set; }

    public string? Status { get; set; }

    public DateTime? NextSessionAt { get; set; }

    public int? CreatedBy { get; set; }
    public int LocationId { get; set; }
}