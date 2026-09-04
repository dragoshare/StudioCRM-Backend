using StudioCRM.Domain.Enums;

namespace StudioCRM.Application.DTOs.Packages;

public class PackageDto
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public decimal Price { get; set; }
    public string Currency { get; set; } = string.Empty;
    public int SessionsLimit { get; set; }
    public int SessionsPerWeek { get; set; }
    public int DurationDays { get; set; }
    public SessionBillingType BillingType { get; set; }
    public int ParticipantsCount { get; set; }
    public int? LocationId { get; set; }
    public string? LocationName { get; set; }
    public bool IsPubliclyAvailable { get; set; }
    public string? PublicSlug { get; set; }
    public bool IsActive { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public int? CreatedBy { get; set; }
}
