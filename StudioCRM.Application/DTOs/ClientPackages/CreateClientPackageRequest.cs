using StudioCRM.Domain.Enums;

namespace StudioCRM.Application.DTOs.ClientPackages;
public class CreateClientPackageRequest
{
    public int ClientId { get; set; }
    public int PackageId { get; set; }

    public string Name { get; set; } = string.Empty;

    public int TotalSessions { get; set; }
    public decimal TotalPrice { get; set; }

    public SessionBillingType ExpectedBillingType { get; set; }

    public DateTime PurchaseDate { get; set; }
    public DateTime? ValidUntil { get; set; }

    public DateTime? PaymentDueDate { get; set; }
}