namespace StudioCRM.Application.DTOs.ClientPortal;

public class ClientOwnerContactDto
{
    public int UserId { get; set; }

    public string FullName { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public string? AvatarUrl { get; set; }

    public string EmailContactUrl { get; set; } = string.Empty;
}
