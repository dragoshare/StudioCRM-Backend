namespace StudioCRM.Application.DTOs.TrainerRates;

public class UpsertTrainerRateDto
{
    public string SessionType { get; set; } = string.Empty;
    public decimal Rate { get; set; }
}