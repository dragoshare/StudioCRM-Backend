namespace StudioCRM.Application.DTOs.Trainers;

public class UpdateTrainerDto
{
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string? Bio { get; set; }
    public string? Phone { get; set; }
    public string? AvatarUrl { get; set; }
    public string Status { get; set; } = "Active";
    public int ExperienceYears { get; set; }
    public decimal HourlyRate { get; set; }
    public List<int> LocationIds { get; set; } = [];
}