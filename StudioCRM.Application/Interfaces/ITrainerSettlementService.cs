using StudioCRM.Application.DTOs.TrainerSettlements;

namespace StudioCRM.Application.Interfaces;

public interface ITrainerSettlementService
{
    Task<TrainerMonthlySettlementDto?> GetMonthlySettlementAsync(int trainerId, int year, int month, int? locationId = null);
    Task<TrainerMonthlySettlementDto?> MarkAsPaidAsync(int trainerId, int year, int month);
    Task<TrainerMonthlySettlementDto?> ReopenAsync(int trainerId, int year, int month);
    Task<TrainerWorkHoursDocumentDto?> GenerateWorkHoursDocumentAsync(int trainerId, int year, int month, int? locationId = null);
}
