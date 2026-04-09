using StudioCRM.Application.DTOs.Trainers;

namespace StudioCRM.Application.Interfaces;

public interface ITrainerService
{
    Task<TrainerDto> CreateAsync(CreateTrainerDto request);
    Task<List<TrainerDto>> GetAllAsync();
    Task<TrainerDto?> GetByIdAsync(int id);
}