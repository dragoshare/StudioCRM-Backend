using System.ComponentModel.DataAnnotations;

namespace StudioCRM.Application.DTOs.Clients;

// Paged mode of GET /api/clients (enabled when page or pageSize is sent).
public class ClientPageFilter
{
    public const string SortValues = "createdDesc|created|name|nameDesc";
    public const string PackageStatusValues = "active|none";

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

public record FilterOptionDto(int Id, string Name);

public class OwnerFilterOptionsDto
{
    public List<FilterOptionDto> Clients { get; set; } = new();
    public List<FilterOptionDto> Trainers { get; set; } = new();
}

public class OwnerFilterOptionsQuery
{
    // Clients: home location or group membership at the location; trainers: assigned to the location.
    public int? LocationId { get; set; }
    [StringLength(100)]
    public string? Search { get; set; }
    [Range(1, 100)]
    public int Limit { get; set; } = 20;

    public void Validate() => Validator.ValidateObject(this, new ValidationContext(this), true);
}
