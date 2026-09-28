namespace StudioCRM.Application.DTOs.Clients;

public class ClientClosurePreviewDto
{
    public int ClientId { get; set; }
    public List<string> Blockers { get; set; } = new();
    public List<int> FutureSessionIds { get; set; } = new();
    public decimal Balance { get; set; }
    public List<ClientClosurePackageDto> Packages { get; set; } = new();
}

public class ClientClosurePackageDto
{
    public int ClientPackageId { get; set; }
    public string Name { get; set; } = "";
    public bool RequiresDecision { get; set; }
    public int RemainingSessions { get; set; }
    public decimal AmountPaid { get; set; }
    public decimal AmountDue { get; set; }
    public string Currency { get; set; } = "PLN";
    public string? Disposition { get; set; }
    public decimal RefundAmount { get; set; }
    public DateTime? RefundConfirmedAt { get; set; }
}

public class CloseClientRequest
{
    public string Reason { get; set; } = "";
    public List<CloseClientPackageRequest> Packages { get; set; } = new();
}

public class CloseClientPackageRequest
{
    public int ClientPackageId { get; set; }
    public string Disposition { get; set; } = "Retain";
    public decimal RefundAmount { get; set; }
}

public class ConfirmClientRefundRequest
{
    public string Reference { get; set; } = "";
    public decimal Amount { get; set; }
}

public class ResumeRetainedPackageRequest
{
    public DateTime ValidUntil { get; set; }
    public string Reason { get; set; } = "";
}
