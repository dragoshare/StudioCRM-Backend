namespace StudioCRM.Application.DTOs.Alerts;

public class OperationalAlertsDto
{
    public int TotalCount => Items.Count;
    public int CriticalCount => Items.Count(i => i.Severity == "Critical");
    public int WarningCount => Items.Count(i => i.Severity == "Warning");
    public int InfoCount => Items.Count(i => i.Severity == "Info");
    public List<OperationalAlertDto> Items { get; set; } = new();
}

public class OperationalAlertDto
{
    public string Type { get; set; } = string.Empty;
    public string Severity { get; set; } = "Info";
    public string Title { get; set; } = string.Empty;
    public string? Message { get; set; }
    public int? ClientId { get; set; }
    public string? ClientName { get; set; }
    public int? TrainerId { get; set; }
    public string? TrainerName { get; set; }
    public int? SessionId { get; set; }
    public int? PaymentId { get; set; }
    public int? InvitationId { get; set; }
    public int? TrainerContractId { get; set; }
    public int? SettlementId { get; set; }
    public int? Year { get; set; }
    public int? Month { get; set; }
    public int? LocationId { get; set; }
    public string? LocationName { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? DueAt { get; set; }
    public string? ActionUrl { get; set; }
}

public class OperationalAlertFilterDto
{
    public int? LocationId { get; set; }
    public string? Type { get; set; }
    public int Limit { get; set; } = 50;
}
