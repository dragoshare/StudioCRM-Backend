namespace StudioCRM.Application.DTOs.Packages;
public class PackageOptionDto
{
    public StudioCRM.Domain.Enums.PackageBillingMode BillingMode { get; set; } = StudioCRM.Domain.Enums.PackageBillingMode.SessionBased;

    public int Id { get; set; }
    public string Name { get; set; } = "";
    public int? LocationId { get; set; }
    public decimal Price { get; set; }
    public string Currency { get; set; } = "";
    public string PackageType { get; set; } = "";
    public string BillingType { get; set; } = "";
    public int ParticipantsCount { get; set; }
    public int SessionsLimit { get; set; }
    public bool IsActive { get; set; }
    /// <summary>Pakiet „Wspierający” (rozpoznawany po nazwie); na listach wyboru jest w osobnej sekcji na końcu.</summary>
    public bool IsSupporting { get; set; }
    /// <summary>Pakiet hybrydowy (rozpoznawany po nazwie); na listach wyboru wszystkie hybrydowe są obok siebie, po treningach na wejścia.</summary>
    public bool IsHybrid { get; set; }
}
