namespace StudioCRM.Application.DTOs.Sessions;

public class SessionFilterDto
{
    public int? TrainerId { get; set; }

    public int? ClientId { get; set; }

    public int? LocationId { get; set; }

    public string? Status { get; set; }

    public DateTime? From { get; set; }

    public DateTime? To { get; set; }

    // History mode: sending Page or PageSize returns a paged envelope (PageSize 1..100, default 25).
    // Without them the endpoint keeps returning the full list sorted by StartAt ascending (schedule).
    public int? Page { get; set; }

    public int? PageSize { get; set; }

    // Paged mode only: StartAt direction, desc (default, newest first) or asc.
    public string? Order { get; set; }

    public bool IsPaged => Page.HasValue || PageSize.HasValue;
}