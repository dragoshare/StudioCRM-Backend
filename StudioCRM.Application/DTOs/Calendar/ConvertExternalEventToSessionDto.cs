namespace StudioCRM.Application.DTOs.Calendar;

public class ConvertExternalEventToSessionDto
{
    public int ClientId { get; set; }

    public int? PackageId { get; set; }

    public int LocationId { get; set; }

    public string Status { get; set; } = "Planned";
}
