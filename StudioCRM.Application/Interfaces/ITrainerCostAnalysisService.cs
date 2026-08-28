using StudioCRM.Application.DTOs.Billing;

namespace StudioCRM.Application.Interfaces;

public interface ITrainerCostAnalysisService
{
    Task<PagedResultDto<TrainerSessionProfitabilityDto>> GetSessionProfitabilityAsync(
        TrainerCostAnalysisFilterDto filter);

    Task<TrainerCostStatisticsDto> GetStatisticsAsync(
        TrainerCostAnalysisFilterDto filter);
}
