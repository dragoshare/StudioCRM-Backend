namespace StudioCRM.Application.DTOs.TrainerRates;

public class UpdateTrainerRatesDto
{
    public List<UpsertTrainerRateDto> Rates { get; set; } = new();
}