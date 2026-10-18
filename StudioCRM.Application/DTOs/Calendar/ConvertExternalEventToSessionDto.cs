namespace StudioCRM.Application.DTOs.Calendar;

// All fields are optional and currently ignored: the session (trainer, location, clients, time) is built from
// the Outlook event itself. Send {}.
public class ConvertExternalEventToSessionDto
{
    public int? ClientId { get; set; }

    public int? PackageId { get; set; }

    public int? LocationId { get; set; }

    public string? Status { get; set; }
}
