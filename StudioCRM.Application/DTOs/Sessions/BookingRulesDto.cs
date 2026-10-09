namespace StudioCRM.Application.DTOs.Sessions;

public class BookingRulesDto
{
    public int RegistrationClosesBeforeMinutes { get; set; }
    public int CancellationClosesBeforeMinutes { get; set; }
    public DateTime RegistrationClosesAtUtc { get; set; }
    public DateTime CancellationClosesAtUtc { get; set; }
    public string LateCancellationPolicy { get; set; } = "ContactStudio";
}
