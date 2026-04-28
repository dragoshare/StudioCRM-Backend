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
    public decimal TotalPrice { get; set; }
    public decimal ExpectedUnitPrice { get; set; }

    public SessionBillingType ExpectedBillingType { get; set; }

    public PaymentStatus PaymentStatus { get; set; }

    public DateTime PurchaseDate { get; set; }
    public DateTime? ValidUntil { get; set; }
    public DateTime? PaidAt { get; set; }
    public DateTime? PaymentDueDate { get; set; }

    public bool IsActive { get; set; } = true;
}