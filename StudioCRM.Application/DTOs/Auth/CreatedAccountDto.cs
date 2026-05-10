namespace StudioCRM.Application.DTOs.Auth;

public class CreatedAccountDto
{
    public int UserId { get; set; }

    public string Email { get; set; } = string.Empty;

    public string Role { get; set; } = string.Empty;

    public int? ClientId { get; set; }

    public int? TrainerId { get; set; }

    public int? LocationId { get; set; }
}
