namespace StudioCRM.Application.DTOs.Public;

public class PublicGroupLocationDto
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string City { get; set; } = string.Empty;

    public string? Address { get; set; }
}

public class PublicGroupPackageDto
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public decimal Price { get; set; }

    public string Currency { get; set; } = string.Empty;

    public int EntriesCount { get; set; }

    public int DurationDays { get; set; }

    public int? LocationId { get; set; }

    public string? LocationName { get; set; }

    public string? PublicSlug { get; set; }
}

public class PublicGroupPurchaseDto
{
    public int ClientPackageId { get; set; }

    public int PackageId { get; set; }

    public string PackageName { get; set; } = string.Empty;

    public decimal AmountDue { get; set; }

    public string Currency { get; set; } = string.Empty;

    public string PaymentStatus { get; set; } = string.Empty;

    public int EntriesCount { get; set; }

    public int RemainingEntries { get; set; }

    public DateTime? ValidUntil { get; set; }

    public string? Message { get; set; }
}

public class PublicGroupClassDto
{
    public int Id { get; set; }

    public string Title { get; set; } = string.Empty;

    public string? Note { get; set; }

    public DateTime StartAt { get; set; }

    public DateTime EndAt { get; set; }

    public int TrainerId { get; set; }

    public string TrainerFullName { get; set; } = string.Empty;

    public int LocationId { get; set; }

    public string LocationName { get; set; } = string.Empty;

    public int Capacity { get; set; }

    public int BookedSeats { get; set; }

    public int AvailableSeats { get; set; }

    public bool IsFullyBooked { get; set; }

    public bool IsBookedByCurrentClient { get; set; }

    public string? PublicSlug { get; set; }
}

public class PublicGroupClassFilterDto
{
    public int? LocationId { get; set; }

    public DateTime? From { get; set; }

    public DateTime? To { get; set; }

    public int Limit { get; set; } = 50;
}

public class PublicGroupRegisterRequest
{
    public string Email { get; set; } = string.Empty;

    public string Password { get; set; } = string.Empty;

    public string FirstName { get; set; } = string.Empty;

    public string LastName { get; set; } = string.Empty;

    public string? PhoneNumber { get; set; }

    public int LocationId { get; set; }
}

public class PublicGroupBookingDto
{
    public int SessionId { get; set; }

    public int ClientId { get; set; }

    public int SessionParticipantId { get; set; }

    public int ClientPackageId { get; set; }

    public string Status { get; set; } = string.Empty;

    public int RemainingEntries { get; set; }
}
