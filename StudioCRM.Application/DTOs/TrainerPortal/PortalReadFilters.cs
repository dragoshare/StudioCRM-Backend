using System.ComponentModel.DataAnnotations;
namespace StudioCRM.Application.DTOs.TrainerPortal;

public class PortalPageFilter
{
    [Range(1, 100000)] public int Page { get; set; } = 1;
    [Range(1, 100)] public int PageSize { get; set; } = 25;
    [StringLength(100)] public string? Search { get; set; }
    public int? LocationId { get; set; }
    [StringLength(30)] public string? Status { get; set; }
    // true: client has at least one active, not closed ClientPackage (non-empty ActivePackages); false: none. Only clients/page.
    public bool? HasActivePackage { get; set; }
    [RegularExpression("^(" + SortValues + ")$")]
    public string Sort { get; set; } = "name";
    public const string SortValues = "name|nameDesc|created|createdDesc|balance|balanceDesc|packageUsage|packageUsageDesc";
    public void Validate()
    {
        Validator.ValidateObject(this, new ValidationContext(this), true);
    }
}
public class GroupClientOptionDto
{
    public int ClientId { get; set; }
    public string FullName { get; set; } = "";
}
