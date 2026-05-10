namespace StudioCRM.Application.DTOs.Subscriptions;

public class SubscriptionDto
{
    public int ClientId { get; set; }
    public string ClientName { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public bool AutoRenewEnabled { get; set; }
    public bool CancelRenewalRequested { get; set; }
    public DateTime? RenewalCancellationRequestedAt { get; set; }
    public SubscriptionCycleDto? CurrentCycle { get; set; }
    public SubscriptionNextPackageDto? NextPackage { get; set; }
    public decimal CarryOverBalance { get; set; }
}

public class SubscriptionCycleDto
{
    public int ClientPackageId { get; set; }
    public int PackageId { get; set; }
    public string PackageName { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public int TotalSessions { get; set; }
    public int UsedSessions { get; set; }
    public int RemainingSessions { get; set; }
    public decimal OriginalPrice { get; set; }
    public decimal BalanceApplied { get; set; }
    public decimal AmountToPay { get; set; }
    public decimal AmountPaid { get; set; }
    public decimal AmountDue { get; set; }
    public string Currency { get; set; } = "PLN";
    public string ExpectedBillingType { get; set; } = string.Empty;
    public string PaymentStatus { get; set; } = string.Empty;
    public DateTime PurchaseDate { get; set; }
    public DateTime? ValidUntil { get; set; }
    public DateTime? ActivatedAt { get; set; }
}

public class SubscriptionNextPackageDto
{
    public int PackageId { get; set; }
    public string PackageName { get; set; } = string.Empty;
    public int SessionsLimit { get; set; }
    public int SessionsPerWeek { get; set; }
    public decimal Price { get; set; }
    public string Currency { get; set; } = "PLN";
    public string BillingType { get; set; } = string.Empty;
}

public class SetNextPackageRequest
{
    public int PackageId { get; set; }
}

public class SubscriptionUsageDto
{
    public int ClientId { get; set; }
    public int? ClientPackageId { get; set; }
    public string ExpectedBillingType { get; set; } = string.Empty;
    public int TotalSessions { get; set; }
    public int UsedSessions { get; set; }
    public int RemainingSessions { get; set; }
    public decimal AdjustmentsTotal { get; set; }
    public int DifferentThanExpectedCount { get; set; }
    public List<SubscriptionUsageBreakdownDto> ActualBreakdown { get; set; } = new();
    public List<SubscriptionUsageSessionDto> Sessions { get; set; } = new();
}

public class SubscriptionUsageBreakdownDto
{
    public string BillingType { get; set; } = string.Empty;
    public int Count { get; set; }
}

public class SubscriptionUsageSessionDto
{
    public int SessionId { get; set; }
    public DateTime Date { get; set; }
    public string TrainerName { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string PlannedBillingType { get; set; } = string.Empty;
    public string ActualBillingType { get; set; } = string.Empty;
    public decimal ExpectedUnitPrice { get; set; }
    public decimal ActualUnitPrice { get; set; }
    public decimal BalanceDifference { get; set; }
}
