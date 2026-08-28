namespace StudioCRM.Application.DTOs.Billing;

public class TrainerCostAnalysisFilterDto
{
    public int? TrainerId { get; set; }
    public int? LocationId { get; set; }
    public int? LegalEntityId { get; set; }
    public int? ClientId { get; set; }
    public int? ClientPackageId { get; set; }
    public bool? IsCoveredByContract { get; set; }
    public DateTime? From { get; set; }
    public DateTime? To { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 25;
}
