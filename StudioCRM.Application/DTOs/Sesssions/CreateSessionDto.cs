namespace StudioCRM.Application.DTOs.Sessions;

public class CreateSessionDto
{
    public string Title { get; set; } = string.Empty;
    public string? Note { get; set; }
    public DateTime StartAt { get; set; }
    public DateTime EndAt { get; set; }

    public int TrainerId { get; set; }
    public int ClientId { get; set; }
    public int? PackageId { get; set; }

    public string? Location { get; set; }
    public string Status { get; set; } = "Planned";

    public int? CreatedBy { get; set; }
}