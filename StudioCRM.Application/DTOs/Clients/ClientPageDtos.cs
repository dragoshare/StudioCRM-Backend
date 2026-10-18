using System.ComponentModel.DataAnnotations;

namespace StudioCRM.Application.DTOs.Clients;

// Paged mode of GET /api/clients (enabled when page or pageSize is sent).
public class ClientPageFilter
{
    public const string SortValues = "createdDesc|created|name|nameDesc|balance|balanceDesc|packageUsage|packageUsageDesc|trainer|trainerDesc|archivedAt|archivedAtDesc|amountDue|amountDueDesc";
    public const string PackageStatusValues = "active|none";

    // Name or email (case-insensitive); with 3+ digits also the phone number, see NormalizePhoneSearch.
    [StringLength(100)]
    public string? Search { get; set; }
    public int? TrainerId { get; set; }
    // Client home location (Client.LocationId), as in /api/clients/filter.
    public int? LocationId { get; set; }
    // false (default): active profiles only; true: archived only.
    public bool IsArchived { get; set; }
    [StringLength(30)]
    public string? Status { get; set; }
    // active: at least one active, not closed package (same set as activePackages); none: no such package.
    [RegularExpression("^(" + PackageStatusValues + ")$")]
    public string? PackageStatus { get; set; }
    // true: only clients with TotalAmountDue > 0; false or missing: no filter.
    public bool? HasAmountDue { get; set; }
    [RegularExpression("^(" + SortValues + ")$")]
    public string? Sort { get; set; }
    [RegularExpression("^(asc|desc)$")]
    public string? Order { get; set; }
    [Range(1, 100000)]
    public int Page { get; set; } = 1;
    [Range(1, 100)]
    public int PageSize { get; set; } = 25;

    public string ResolveSort()
    {
        var sort = Sort ?? "createdDesc";
        if (Order is null) return sort;
        var field = sort.EndsWith("Desc") ? sort[..^4] : sort;
        return Order == "desc" ? field + "Desc" : field;
    }

    public void Validate() => Validator.ValidateObject(this, new ValidationContext(this), true);

    // Digits of the search term used to match Client.PhoneNumber compared digits-only; null below 3 digits.
    // A Polish prefix (+48 / 0048 before 9 digits) is dropped so "+48 600-100-200" finds "600100200" and "+48600100200".
    public static string? NormalizePhoneSearch(string? search)
    {
        if (string.IsNullOrWhiteSpace(search)) return null;
        var digits = new string(search.Where(char.IsAsciiDigit).ToArray());
        if (digits.Length == 13 && digits.StartsWith("0048")) digits = digits[4..];
        else if (digits.Length == 11 && digits.StartsWith("48")) digits = digits[2..];
        return digits.Length >= 3 ? digits : null;
    }
}

public class ClientPageSummaryDto
{
    // Clients in the filtered set with Client.Status == "Active".
    public int ActiveClientsCount { get; set; }
    // Clients in the filtered set with at least one active, not closed package.
    public int WithActivePackageCount { get; set; }
}

public class ClientPageDto
{
    public List<ClientDto> Items { get; set; } = new();
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalCount { get; set; }
    public int TotalPages { get; set; }
    public ClientPageSummaryDto Summary { get; set; } = new();
}

public record FilterOptionDto(int Id, string Name)
{
    // Client.Status or Trainer.Status.
    public string? Status { get; init; }
    public bool IsArchived { get; init; }
    // True only for selectedClientId/selectedTrainerId; such a row may lie outside the filter, the limit or the archive.
    public bool IsSelected { get; init; }
    // Trainer details, only with includeTrainerDetails=true.
    public List<int>? LocationIds { get; init; }
    public string? AvatarUrl { get; init; }
    public string? Color { get; init; }
}

public class OwnerFilterOptionsDto
{
    public List<FilterOptionDto> Clients { get; set; } = new();
    public List<FilterOptionDto> Trainers { get; set; } = new();
}

public class OwnerFilterOptionsQuery
{
    // Clients: home location or group membership at the location; trainers: assigned to the location.
    public int? LocationId { get; set; }
    // Name or email; for clients also phone digits (as in GET /api/clients).
    [StringLength(100)]
    public string? Search { get; set; }
    [Range(1, 100)]
    public int Limit { get; set; } = 20;
    // Exact Client.Status (e.g. Active), applied before Limit.
    [StringLength(30)]
    public string? ClientStatus { get; set; }
    // Exact Trainer.Status (e.g. Active), applied before Limit.
    [StringLength(30)]
    public string? TrainerStatus { get; set; }
    // true: clients with group access enabled at LocationId; false: without it. Requires LocationId; replaces the location rule above.
    public bool? GroupAccess { get; set; }
    public int? SelectedClientId { get; set; }
    public int? SelectedTrainerId { get; set; }
    public bool IncludeTrainerDetails { get; set; }

    public void Validate()
    {
        Validator.ValidateObject(this, new ValidationContext(this), true);
        if (GroupAccess.HasValue && !LocationId.HasValue)
            throw new ValidationException("LocationId is required for GroupAccess.");
    }
}
