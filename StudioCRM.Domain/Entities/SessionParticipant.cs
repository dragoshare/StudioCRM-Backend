namespace StudioCRM.Domain.Entities;

public class SessionParticipant
{
    public int Id { get; set; }

    public int SessionId { get; set; }

    public int ClientId { get; set; }

    public int? PackageId { get; set; }

    public string AttendanceStatus { get; set; } = "Planned";
    // Planned, Present, NoShow, CancelledInTime, CancelledLate

    public bool CountsAgainstPackage { get; set; } = true;

    public int SessionsCharged { get; set; } = 1;

    public string? Note { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public Session Session { get; set; } = null!;

    public Client Client { get; set; } = null!;

    public Package? Package { get; set; }
}