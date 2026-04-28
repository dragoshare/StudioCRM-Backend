namespace StudioCRM.Application.DTOs.ClientPortal;
public class ClientCountedSessionDto
{
    public int SessionId { get; set; }

    public DateTime Date { get; set; }

    public string TrainerName { get; set; } = string.Empty;
    public string? LocationName { get; set; }

    public string Status { get; set; } = string.Empty;

    public bool WasCountedFromPackage { get; set; }

    public string PlannedBillingType { get; set; } = string.Empty;
    public string ActualBillingType { get; set; } = string.Empty;

    public decimal ExpectedUnitPrice { get; set; }
    public decimal ActualUnitPrice { get; set; }
    public decimal BalanceDifference { get; set; }

    public string Description { get; set; } = string.Empty;
}