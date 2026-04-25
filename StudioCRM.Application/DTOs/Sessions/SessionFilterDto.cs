namespace StudioCRM.Application.DTOs.Sessions;

public class SessionFilterDto
{
    public int? TrainerId { get; set; }

    public int? ClientId { get; set; }

    public int? LocationId { get; set; }

    public string? Status { get; set; }

    public DateTime? From { get; set; }

    public DateTime? To { get; set; }
}