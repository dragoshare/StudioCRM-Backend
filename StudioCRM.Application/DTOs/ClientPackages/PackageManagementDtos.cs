namespace StudioCRM.Application.DTOs.ClientPackages;

public class PackageChangeRequest
{
    public string ExpectedVersion { get; set; } = "";
    public string Reason { get; set; } = "";
    public int? TotalSessions { get; set; }
    public decimal? TotalPrice { get; set; }
    public DateTime? ValidUntil { get; set; }
    public DateTime? PaymentDueDate { get; set; }
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
    public int RemainingSessions { get; set; }
    public bool CanDelete { get; set; }
    public bool CanEdit { get; set; }
    public bool CanCorrect { get; set; }
    public bool CanClose { get; set; }
    public string? DeleteBlockReason { get; set; }
    public List<string> Blockers { get; set; } = new();
}
