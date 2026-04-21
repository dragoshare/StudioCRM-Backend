namespace StudioCRM.Application.DTOs.Packages;

public class UpdatePackageDto
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public decimal Price { get; set; }
    public string Currency { get; set; } = "PLN";
    public int SessionsLimit { get; set; }
    public int DurationDays { get; set; }
    public bool IsActive { get; set; }
}