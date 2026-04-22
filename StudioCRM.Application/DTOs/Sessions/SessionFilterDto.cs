namespace StudioCRM.Application.DTOs.Sessions;

public class SessionFilterDto
{
    public int? TrainerId { get; set; }
    public int? ClientId { get; set; }
    public DateTime? DateFrom { get; set; }
    public DateTime? DateTo { get; set; }
    public string? Status { get; set; }
    public int? LocationId { get; set; }
}