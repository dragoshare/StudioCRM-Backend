namespace StudioCRM.Application.DTOs.ClientPortal;

public class ClientTrainerContactDto
{
    public int TrainerId { get; set; }

    public string FullName { get; set; } = string.Empty;

    public string? Email { get; set; }

    public string? EmailContactUrl { get; set; }

    public string? Phone { get; set; }

    public string? PhoneContactUrl { get; set; }

    public string? Bio { get; set; }

    public string? AvatarUrl { get; set; }

    public int ExperienceYears { get; set; }
}
