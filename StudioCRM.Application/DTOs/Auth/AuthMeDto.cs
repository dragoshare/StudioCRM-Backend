namespace StudioCRM.Application.DTOs.Auth;

public class AuthMeDto
{
    public int UserId { get; set; }

    public string Email { get; set; } = string.Empty;

    public string Role { get; set; } = string.Empty;

    public List<string> Roles { get; set; } = new();

    public string FullName { get; set; } = string.Empty;

    public string? AvatarUrl { get; set; }

    public int? ClientId { get; set; }

    public int? TrainerId { get; set; }

    public int? LocationId { get; set; }

    public string? ClientSource { get; set; }

    public string? PortalAccessMode { get; set; }
}
