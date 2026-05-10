namespace StudioCRM.Application.DTOs.Invitations;

public class InvitationDto
{
    public int Id { get; set; }

    public string Email { get; set; } = string.Empty;

    public string Role { get; set; } = string.Empty;

    public int LocationId { get; set; }

    public string LocationName { get; set; } = string.Empty;

    public string Token { get; set; } = string.Empty;

    public string InviteLink { get; set; } = string.Empty;

    public DateTime ExpiresAt { get; set; }

    public bool IsAccepted { get; set; }

    public DateTime? AcceptedAt { get; set; }

    public DateTime? CancelledAt { get; set; }

    public DateTime CreatedAt { get; set; }

    public string Status { get; set; } = string.Empty;
}
