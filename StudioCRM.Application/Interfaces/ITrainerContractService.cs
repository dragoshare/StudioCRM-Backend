using StudioCRM.Application.DTOs.TrainerContracts;

namespace StudioCRM.Application.Interfaces;

public interface ITrainerContractService
{
    Task<List<TrainerContractDto>> GetByTrainerIdAsync(int trainerId);
    Task<TrainerContractDto?> GetByIdAsync(int trainerId, int contractId);
    Task<TrainerContractDto> CreateAsync(int trainerId, CreateTrainerContractDto request);
    Task<TrainerContractDto?> UpdateAsync(int trainerId, int contractId, UpdateTrainerContractDto request);
}
