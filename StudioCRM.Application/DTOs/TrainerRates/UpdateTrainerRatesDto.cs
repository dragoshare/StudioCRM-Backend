namespace StudioCRM.Application.DTOs.TrainerRates;

public class UpdateTrainerRatesDto
{
    public decimal? HourlyRate { get; set; }

    public List<UpsertTrainerRateDto> Rates { get; set; } = new();
}
