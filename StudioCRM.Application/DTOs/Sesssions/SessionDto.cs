namespace StudioCRM.Application.DTOs.Sessions;

public class SessionDto
{
    public int Id { get; set; }

    public string Title { get; set; } = string.Empty;
    public string? Note { get; set; }
    public DateTime StartAt { get; set; }
    public DateTime EndAt { get; set; }

    public int TrainerId { get; set; }
    public int ClientId { get; set; }
    public int? PackageId { get; set; }

    public string? TrainerFullName { get; set; }
    public string? ClientFullName { get; set; }
    public string? PackageName { get; set; }

    public string? Location { get; set; }
    public string Status { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public int? CreatedBy { get; set; }
}