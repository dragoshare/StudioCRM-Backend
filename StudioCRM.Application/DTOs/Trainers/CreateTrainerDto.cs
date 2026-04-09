namespace StudioCRM.Application.DTOs.Trainers;

public class CreateTrainerDto
{
    public string Email { get; set; } = string.Empty;

    public string Password { get; set; } = string.Empty;

    public string FirstName { get; set; } = string.Empty;

    public string LastName { get; set; } = string.Empty;

    public string PhoneNumber { get; set; } = string.Empty;

    public string Specialization { get; set; } = string.Empty;

    public string EmploymentType { get; set; } = string.Empty;

    public decimal HourlyRate { get; set; }
}