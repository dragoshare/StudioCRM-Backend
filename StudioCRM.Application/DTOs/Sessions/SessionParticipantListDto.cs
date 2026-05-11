namespace StudioCRM.Application.DTOs.Sessions;

public class SessionParticipantListDto
{
    public int Id { get; set; }

    public int ClientId { get; set; }

    public string ClientFullName { get; set; } = string.Empty;

    public int? PackageId { get; set; }

    public string? PackageName { get; set; }

    public int? ClientPackageId { get; set; }

    public string AttendanceStatus { get; set; } = string.Empty;

    public bool CountsAgainstPackage { get; set; }

    public bool IsCountedFromPackage { get; set; }

    public int SessionsCharged { get; set; }

    public string PlannedBillingType { get; set; } = string.Empty;

    public string ActualBillingType { get; set; } = string.Empty;

    public decimal ExpectedUnitPrice { get; set; }

    public decimal ActualUnitPrice { get; set; }

    public decimal BalanceDifference { get; set; }

    public string? Note { get; set; }
}
