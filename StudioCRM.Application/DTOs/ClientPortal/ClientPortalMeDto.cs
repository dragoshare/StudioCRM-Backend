namespace StudioCRM.Application.DTOs.ClientPortal;

public class ClientPortalMeDto
{
    public int ClientId { get; set; }

    public string FirstName { get; set; } = string.Empty;

    public string LastName { get; set; } = string.Empty;

    public string FullName { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public string? PhoneNumber { get; set; }

    public string? AvatarUrl { get; set; }

    public string? Goal { get; set; }

    public string Status { get; set; } = string.Empty;

    public string BillingStatus { get; set; } = string.Empty;

    public string Source { get; set; } = string.Empty;

    public string PortalAccessMode { get; set; } = string.Empty;

    public string LocationName { get; set; } = string.Empty;

    public string? TrainerFullName { get; set; }
}
