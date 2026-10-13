namespace StudioCRM.Application.DTOs.SessionParticipants;

public record GroupPackageOptionsDto(int SessionId, int ClientId, int LocationId, DateTime SessionStartAt,
    List<GroupPackageOfferDto> Offers, List<OwnedGroupPackageDto> OwnedPackages);
public record GroupPackageOfferDto(int PackageId, string Name, decimal Price, string Currency,
    int SessionsLimit, int DurationDays, bool CanSelect, List<string> Reasons);
public record OwnedGroupPackageDto(int ClientPackageId, int PackageId, string Name, decimal Price, string Currency,
    decimal AmountDue, int? LocationId, DateTime? ValidUntil, string PaymentStatus, bool IsActive,
    int TotalEntries, int UsedEntries, int ReservedEntries, int AvailableEntries,
    bool IsEligible, bool CanSelect, List<string> Reasons);
