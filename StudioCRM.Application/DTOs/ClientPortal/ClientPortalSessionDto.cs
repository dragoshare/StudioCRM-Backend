namespace StudioCRM.Application.DTOs.ClientPortal;

public class ClientPortalSessionDto
{
    public int Id { get; set; }

    public string Title { get; set; } = string.Empty;

    public string? Note { get; set; }

    public DateTime StartAt { get; set; }

    public DateTime EndAt { get; set; }

    public string LocationName { get; set; } = string.Empty;

    public string? TrainerFullName { get; set; }

    public string Status { get; set; } = string.Empty;
}
