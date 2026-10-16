namespace StudioCRM.Application.DTOs.SessionParticipants;

public record GroupPackageOptionsDto(int SessionId, int ClientId, int LocationId, DateTime SessionStartAt,
    List<GroupPackageOfferDto> Offers, List<OwnedGroupPackageDto> OwnedPackages);
public record GroupPackageOfferDto(int PackageId, string Name, decimal Price, string Currency,
    int SessionsLimit, int DurationDays, bool CanSelect, List<string> Reasons)
{
    public StudioCRM.Domain.Enums.PackageBillingMode BillingMode { get; init; }
    public string? Description { get; init; }
    public int SessionsPerWeek { get; init; }
    public int ParticipantsCount { get; init; }
    // null = offer valid in every location.
    public int? LocationId { get; init; }
    public string? LocationName { get; init; }
    public bool IsPubliclyAvailable { get; init; }
    // Same rounding as ClientPackage.ExpectedUnitPrice created from this offer.
    public decimal UnitPrice { get; init; }
}
public record OwnedGroupPackageDto(int ClientPackageId, int PackageId, string Name, decimal Price, string Currency,
    decimal AmountDue, int? LocationId, DateTime? ValidUntil, string PaymentStatus, bool IsActive,
    int TotalEntries, int UsedEntries, int ReservedEntries, int AvailableEntries,
    bool IsEligible, bool CanSelect, List<string> Reasons)
{
    public StudioCRM.Domain.Enums.PackageBillingMode BillingMode { get; init; }
}
