namespace StudioCRM.Application.DTOs.Invitations;

public class InvitationFilterDto
{
    public string? Status { get; set; }
    public string? Role { get; set; }
    public int? LocationId { get; set; }
    public int? TrainerId { get; set; }
    public string? Search { get; set; }
}
