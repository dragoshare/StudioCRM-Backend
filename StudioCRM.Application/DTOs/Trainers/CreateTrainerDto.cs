namespace StudioCRM.Application.DTOs.Trainers;

public class CreateTrainerDto
{
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;

    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;

    public string? Bio { get; set; }
    public string? Phone { get; set; }
    public string? AvatarUrl { get; set; }
    public string Status { get; set; } = "Active";
    public int ExperienceYears { get; set; }

    public int? CreatedBy { get; set; }
    public List<int> LocationIds { get; set; } = [];
}