namespace StudioCRM.Application.DTOs.TrainerPortal;

public class TrainerPortalMeDto
{
    public int TrainerId { get; set; }

    public int UserId { get; set; }

    public string FullName { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public string? Phone { get; set; }

    public string? AvatarUrl { get; set; }

    public string? Bio { get; set; }

    public string Status { get; set; } = string.Empty;

    public int ExperienceYears { get; set; }

    public List<int> LocationIds { get; set; } = new();

    public List<string> LocationNames { get; set; } = new();
}
