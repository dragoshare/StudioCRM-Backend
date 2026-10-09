namespace StudioCRM.Application.DTOs.TrainerPortal;

public class TrainerPortalSessionDto
{
    public string? EventRules { get; set; }
    public StudioCRM.Application.DTOs.Sessions.BookingRulesDto BookingRules { get; set; } = new();
    public bool IsGroupSession { get; set; }
    public int? Capacity { get; set; }
    public int BookedSeats { get; set; }
    public int? AvailableSeats { get; set; }
    public bool IsFullyBooked { get; set; }
    public int LocationId { get; set; }
    public string? PlannedSessionType { get; set; }
    public string? ActualSessionType { get; set; }
    public bool IsPubliclyBookable { get; set; }
    public List<TrainerSessionParticipantDto> Participants { get; set; } = new();

    public int SessionId { get; set; }

    public string Title { get; set; } = string.Empty;

    public string? Note { get; set; }

    public DateTime StartAt { get; set; }

    public DateTime EndAt { get; set; }

    public int TrainerId { get; set; }

    public string TrainerFullName { get; set; } = string.Empty;

    public bool CanEdit { get; set; }

    public string ClientFullName { get; set; } = string.Empty;

    public string LocationName { get; set; } = string.Empty;

    public string Status { get; set; } = string.Empty;
}
