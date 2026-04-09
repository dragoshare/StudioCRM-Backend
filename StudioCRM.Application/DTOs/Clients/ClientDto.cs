namespace StudioCRM.Application.DTOs.Clients;

public class ClientDto
{
    public int Id { get; set; }

    public int? TrainerId { get; set; }

    public string FirstName { get; set; } = string.Empty;

    public string LastName { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public string PhoneNumber { get; set; } = string.Empty;

    public bool IsActive { get; set; }

    public string? TrainerFullName { get; set; }
}