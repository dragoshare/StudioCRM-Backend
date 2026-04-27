namespace StudioCRM.Application.DTOs.TrainerRates;

public class TrainerRateDto
{
    public int Id { get; set; }
    public int TrainerId { get; set; }

    public string SessionType { get; set; } = string.Empty;

    public decimal Rate { get; set; }

    public DateTime ValidFrom { get; set; }
    public DateTime? ValidTo { get; set; }

    public bool IsActive { get; set; }
}