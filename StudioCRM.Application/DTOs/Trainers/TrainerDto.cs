namespace StudioCRM.Application.DTOs.Trainers;

public class TrainerDto
{
    public int Id { get; set; }
    public int UserId { get; set; }

    public string Email { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;

    public string? Bio { get; set; }
    public string? Phone { get; set; }
    public string? AvatarUrl { get; set; }
    public string Status { get; set; } = string.Empty;
    public int ExperienceYears { get; set; }

    public decimal RatingAverage { get; set; }
    public int SessionsCount { get; set; }
    public int ActiveClientsCount { get; set; }

    public decimal HourlyRate { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public int? CreatedBy { get; set; }
}