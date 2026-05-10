using StudioCRM.Domain.Enums;

namespace StudioCRM.Application.DTOs.Packages;

public class CreatePackageDto
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public decimal Price { get; set; }
    public string Currency { get; set; } = "PLN";
    public int SessionsLimit { get; set; }
    public int SessionsPerWeek { get; set; } = 1;
    public int DurationDays { get; set; }
    public SessionBillingType BillingType { get; set; } = SessionBillingType.OneToOne;
    public int? LocationId { get; set; }
    public bool IsActive { get; set; } = true;
    public int? CreatedBy { get; set; }
}
