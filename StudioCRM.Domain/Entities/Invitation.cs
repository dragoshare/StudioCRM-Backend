namespace StudioCRM.Domain.Entities;

public class Invitation
{
    public int Id { get; set; }

    public string Email { get; set; } = string.Empty;

    public string Token { get; set; } = string.Empty;

    public string Role { get; set; } = string.Empty;

    public int LocationId { get; set; }

    public int? TrainerId { get; set; }

    public DateTime ExpiresAt { get; set; }

    public bool IsAccepted { get; set; } = false;

    public DateTime? AcceptedAt { get; set; }

    public DateTime? CancelledAt { get; set; }

    public DateTime? LastSentAt { get; set; }

    public string? LastSendError { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public int CreatedBy { get; set; }

    public Location Location { get; set; } = null!;

    public Trainer? Trainer { get; set; }
}
