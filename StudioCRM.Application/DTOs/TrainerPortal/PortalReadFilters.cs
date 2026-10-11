using System.ComponentModel.DataAnnotations;
namespace StudioCRM.Application.DTOs.TrainerPortal;

public class PortalPageFilter
{
    [Range(1, 100000)] public int Page { get; set; } = 1;
    [Range(1, 100)] public int PageSize { get; set; } = 25;
    [StringLength(100)] public string? Search { get; set; }
    public int? LocationId { get; set; }
    [StringLength(30)] public string? Status { get; set; }
    [RegularExpression("^(name|nameDesc|created|createdDesc)$")]
    public string Sort { get; set; } = "name";
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
