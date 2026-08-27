namespace StudioCRM.Application.DTOs.Billing;

public class LegalEntityDto
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? Nip { get; set; }

    public string? Address { get; set; }

    public string? Email { get; set; }

    public string? Phone { get; set; }

    public bool IsActive { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }
}
