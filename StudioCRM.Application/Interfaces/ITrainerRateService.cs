using StudioCRM.Application.DTOs.TrainerRates;

namespace StudioCRM.Application.Interfaces;

public interface ITrainerRateService
{
    Task<List<TrainerRateDto>> GetByTrainerIdAsync(int trainerId);
    Task<List<TrainerRateDto>> UpdateRatesAsync(int trainerId, UpdateTrainerRatesDto request);
}