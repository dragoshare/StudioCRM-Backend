namespace StudioCRM.Application.DTOs.Clients;

public class UpdateClientDto
{
    public int? TrainerId { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? PhoneNumber { get; set; }
    public string? AvatarUrl { get; set; }
    public string? Goal { get; set; }
    public string? Notes { get; set; }
    public string BillingStatus { get; set; } = "Pending";
    public string Status { get; set; } = "New";
    public DateTime? NextSessionAt { get; set; }
    public int LocationId { get; set; }
}
