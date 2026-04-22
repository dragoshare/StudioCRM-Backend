namespace StudioCRM.Application.DTOs.Sessions;

public class UpdateSessionDto
{
    public string Title { get; set; } = string.Empty;

    public string? Note { get; set; }

    public DateTime StartAt { get; set; }

    public DateTime EndAt { get; set; }

    public int TrainerId { get; set; }

    public int ClientId { get; set; }

    public int? PackageId { get; set; }

    public string? StudioRoom { get; set; }

    public int LocationId { get; set; }

    public string Status { get; set; } = "Planned";
}