namespace StudioCRM.Application.DTOs.ClientPackages;
public class ExpiredPackageDecisionRequest
{
    public string Action { get; set; } = ""; // Extend or End
    public DateTime? ValidUntil { get; set; }
    public string ExpectedVersion { get; set; } = "";
    public string Reason { get; set; } = "";
}
