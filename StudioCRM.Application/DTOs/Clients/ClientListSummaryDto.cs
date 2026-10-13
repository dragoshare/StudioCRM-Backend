using StudioCRM.Domain.Enums;
namespace StudioCRM.Application.DTOs.Clients;

public class ClientListSummaryDto
{
    public int? ActivePackageId { get; set; }

    public int? ActiveClientPackageId { get; set; }

    public string? ActiveClientPackageName { get; set; }

    public int ActivePackageTotalSessions { get; set; }

    public int ActivePackageUsedSessions { get; set; }

    public int ActivePackageRemainingSessions { get; set; }

    public string ActivePackagePaymentStatus { get; set; } = string.Empty;

    public decimal CurrentBalance { get; set; }

    public List<ClientListPackageDto> ActivePackages { get; set; } = new();
}

public class ClientListPackageDto
{
    public int ClientPackageId { get; set; }
    public int PackageId { get; set; }
    public string Name { get; set; } = "";
    public PackageBillingMode BillingMode { get; set; }
    public string Status { get; set; } = "";
    public string PaymentStatus { get; set; } = "";
    public int? TotalSessions { get; set; }
    public int? UsedSessions { get; set; }
    public int? RemainingSessions { get; set; }
    public DateTime? ValidUntil { get; set; }
    public DateTime? PeriodStartsAt { get; set; }
    public int? LocationId { get; set; }
}
