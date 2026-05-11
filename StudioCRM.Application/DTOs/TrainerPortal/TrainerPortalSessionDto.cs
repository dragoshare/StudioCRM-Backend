namespace StudioCRM.Application.DTOs.TrainerPortal;

public class TrainerPortalSessionDto
{
    public int SessionId { get; set; }

    public string Title { get; set; } = string.Empty;

    public string? Note { get; set; }

    public DateTime StartAt { get; set; }

    public DateTime EndAt { get; set; }

    public string ClientFullName { get; set; } = string.Empty;

    public string LocationName { get; set; } = string.Empty;

    public string Status { get; set; } = string.Empty;
}
