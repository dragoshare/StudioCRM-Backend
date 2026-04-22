namespace StudioCRM.Application.DTOs.Invitations;

public class CreateInvitationDto
{
    public string Email { get; set; } = string.Empty;

    public string Role { get; set; } = string.Empty;

    public int LocationId { get; set; }
}