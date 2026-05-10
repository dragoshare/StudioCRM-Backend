using StudioCRM.Domain.Enums;

namespace StudioCRM.Domain.Entities;

public class ClientPackage
{
    public int Id { get; set; }

    public int ClientId { get; set; }
    public Client Client { get; set; } = null!;

    public int PackageId { get; set; }
    public Package Package { get; set; } = null!;

    public string Name { get; set; } = string.Empty;

    public int TotalSessions { get; set; }
    public int SessionsPerWeek { get; set; } = 1;
    public int UsedSessions { get; set; }
    public decimal TotalPrice { get; set; }
    public decimal OriginalPrice { get; set; }
    public decimal BalanceApplied { get; set; }
    public decimal AmountPaid { get; set; }
    public decimal ExpectedUnitPrice { get; set; }
    public string Currency { get; set; } = "PLN";

    public int? LocationId { get; set; }
    public Location? Location { get; set; }

    public SessionBillingType ExpectedBillingType { get; set; }

    public PaymentStatus PaymentStatus { get; set; }

    public DateTime PurchaseDate { get; set; }
    public DateTime? ValidUntil { get; set; }
    public DateTime? PaidAt { get; set; }
    public DateTime? PaymentDueDate { get; set; }
    public DateTime? ActivatedAt { get; set; }

    public ClientPackageActivationMode ActivationMode { get; set; } = ClientPackageActivationMode.Immediately;
    public int? PreviousClientPackageId { get; set; }
    public string RenewalSource { get; set; } = "Manual";
    public int? RequestedByUserId { get; set; }
    public int? ActivatedByUserId { get; set; }

    public bool IsActive { get; set; } = true;

    public ICollection<ClientPayment> Payments { get; set; } = new List<ClientPayment>();
    public ICollection<ClientBalanceTransaction> BalanceTransactions { get; set; } = new List<ClientBalanceTransaction>();
}
