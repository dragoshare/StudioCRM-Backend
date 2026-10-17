namespace StudioCRM.Application.DTOs.ClientPackages;

public class PackageChangeRequest
{
    public string ExpectedVersion { get; set; } = "";
    public string Reason { get; set; } = "";
    public int? TotalSessions { get; set; }
    public decimal? TotalPrice { get; set; }
    public DateTime? ValidUntil { get; set; }
    public DateTime? PaymentDueDate { get; set; }
    // Required when totalPrice drops below the amount already paid: ToBalance or KeepByStudio.
    public string? SurplusDisposition { get; set; }
}

public class PackageCorrectionResultDto
{
    public bool Corrected { get; set; } = true;
    public decimal Surplus { get; set; }
    public string? SurplusDisposition { get; set; }
    public decimal AmountPaid { get; set; }
    public decimal AmountDue { get; set; }
}

public class ClosePackageRequest
{
    public string ExpectedVersion { get; set; } = "";
    public string Reason { get; set; } = "";
    public string DebtDisposition { get; set; } = "KeepDue";
    public string FundsDisposition { get; set; } = "KeepFunds";
    public decimal SettlementAmount { get; set; }
    public CreateClientPackageRequest? Replacement { get; set; }
}

public class ImportClientPackageRequest
{
    public Guid RequestId { get; set; }
    public CreateClientPackageRequest Package { get; set; } = new();
    public int UsedSessions { get; set; }
    public decimal AmountPaid { get; set; }
    public string Reason { get; set; } = "";
}

public class PackageManagementPreviewDto
{
    public int ClientPackageId { get; set; }
    public string Version { get; set; } = "";
    public decimal AmountDue { get; set; }
    public decimal AmountPaid { get; set; }
    public decimal TotalPrice { get; set; }
    public int RemainingSessions { get; set; }
    public bool CanDelete { get; set; }
    public bool CanEdit { get; set; }
    public bool CanCorrect { get; set; }
    // Price-only corrections ignore renewal and planned-session blockers.
    public bool CanCorrectPrice { get; set; }
    public bool CanClose { get; set; }
    public string? DeleteBlockReason { get; set; }
    public List<string> Blockers { get; set; } = new();
}

// Owner-only record of an already finished personal package entered after the fact (e.g. a payment for an earlier
// package that was never registered). Created inactive with all entries used; does not touch the active package or balance.
public class HistoricalClientPackageRequest
{
    public Guid RequestId { get; set; }
    public string Reason { get; set; } = "";
    public int ClientId { get; set; }
    public int PackageId { get; set; }
    public string? Name { get; set; }
    public int? TotalSessions { get; set; }
    public decimal? TotalPrice { get; set; }
    public DateTime PurchaseDate { get; set; }
    // Defaults to the purchase day; must not be later than today.
    public DateTime? ValidUntil { get; set; }
    public DateTime? PaymentDueDate { get; set; }
}
