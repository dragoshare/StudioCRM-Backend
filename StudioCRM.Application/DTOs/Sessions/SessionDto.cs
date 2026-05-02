namespace StudioCRM.Application.DTOs.Sessions;

public class SessionDto
{
    public int Id { get; set; }

    public string Title { get; set; } = string.Empty;

    public string? Note { get; set; }

    public DateTime StartAt { get; set; }

    public DateTime EndAt { get; set; }

    public int TrainerId { get; set; }

    public string TrainerFullName { get; set; } = string.Empty;

    public int LocationId { get; set; }

    public string LocationName { get; set; } = string.Empty;

    public string? StudioRoom { get; set; }

    public string Status { get; set; } = string.Empty;

    public string? PlannedSessionType { get; set; }

    public string? ActualSessionType { get; set; }

    public int? ActualParticipantsCount { get; set; }

    public DateTime? CompletedAt { get; set; }

    public int ParticipantsCount { get; set; }

    public string ClientsDisplayName { get; set; } = string.Empty;

    public List<SessionParticipantListDto> Participants { get; set; } = new();

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public int? CreatedBy { get; set; }
    public int RoomParticipantsCount { get; set; }
    public int RoomLimit { get; set; } = 8;
    public bool IsRoomLimitExceeded { get; set; }
}