namespace StudioCRM.Application.DTOs.Trainers;

public class TrainerDto
{
    public int Id { get; set; }

    public int UserId { get; set; }

    public string Email { get; set; } = string.Empty;

    public string FirstName { get; set; } = string.Empty;

    public string LastName { get; set; } = string.Empty;

    public string PhoneNumber { get; set; } = string.Empty;

    public string Specialization { get; set; } = string.Empty;

    public string EmploymentType { get; set; } = string.Empty;

    public decimal HourlyRate { get; set; }

    public bool IsActive { get; set; }
}
