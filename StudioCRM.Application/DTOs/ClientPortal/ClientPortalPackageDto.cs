namespace StudioCRM.Application.DTOs.ClientPortal;

public class ClientPortalPackageDto
{
    public int? ClientPackageId { get; set; }
    public string? PackageType { get; set; }
    public DateTime? NextSessionAt { get; set; }

    public int? PackageId { get; set; }

    public string? Name { get; set; }

    public string? Description { get; set; }

    public decimal? Price { get; set; }

    public string? Currency { get; set; }

    public int? SessionsLimit { get; set; }

    public int UsedSessionsCount { get; set; }

    public int RemainingSessionsCount { get; set; }

    public int ProgressPercent { get; set; }

    public int? DurationDays { get; set; }
}