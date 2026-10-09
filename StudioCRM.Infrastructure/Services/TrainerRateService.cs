using Microsoft.EntityFrameworkCore;
using StudioCRM.Application.DTOs.TrainerRates;
using StudioCRM.Application.Interfaces;
using StudioCRM.Domain.Entities;
using StudioCRM.Infrastructure.Persistence;

namespace StudioCRM.Infrastructure.Services;

public class TrainerRateService : ITrainerRateService
{
    private readonly StudioCRMDbContext _context;

    public TrainerRateService(StudioCRMDbContext context)
    {
        _context = context;
    }

    public async Task<List<TrainerRateDto>> GetByTrainerIdAsync(int trainerId)
    {
        return await _context.TrainerRates
            .Where(r => r.TrainerId == trainerId && r.IsActive)
            .OrderByDescending(r => r.ValidFrom)
            .Select(r => new TrainerRateDto
            {
                Id = r.Id,
                TrainerId = r.TrainerId,
                SessionType = r.SessionType,
                Rate = r.Rate,
                ValidFrom = r.ValidFrom,
                ValidTo = r.ValidTo,
                IsActive = r.IsActive
            })
            .ToListAsync();
    }

    public async Task<List<TrainerRateDto>> UpdateRatesAsync(int trainerId, UpdateTrainerRatesDto request)
    {
        var trainerExists = await _context.Trainers.AnyAsync(t => t.Id == trainerId);

        if (!trainerExists)
            throw new InvalidOperationException("Trainer does not exist.");

        ValidateRates(request);

        var now = DateTime.UtcNow;

        var activeRates = await _context.TrainerRates
            .Where(r => r.TrainerId == trainerId && r.IsActive)
            .ToListAsync();
        foreach (var (type, value) in new[] { ("Hourly", request.HourlyRate), ("Group", request.GroupSessionRate) })
        {
            if (!value.HasValue)
                continue;

            var currentRates = activeRates.Where(r => r.SessionType == type).ToList();
            if (currentRates.Count == 1 && currentRates[0].Rate == value.Value)
                continue;

            // Preserve initial hourly setup behavior. Group rates only apply prospectively.
            var hasHistory = await _context.TrainerRates
                .AnyAsync(r => r.TrainerId == trainerId && r.SessionType == type);
            var validFrom = type == "Hourly" && !hasHistory
                ? await ResolveInitialRateValidFromAsync(trainerId, now)
                : now;

            foreach (var oldRate in currentRates)
            {
                oldRate.IsActive = false;
                oldRate.ValidTo = validFrom;
                oldRate.UpdatedAt = now;
            }

            await _context.TrainerRates.AddAsync(new TrainerRate
            {
                TrainerId = trainerId,
                SessionType = type,
                Rate = value.Value,
                ValidFrom = validFrom,
                IsActive = true,
                CreatedAt = now,
                UpdatedAt = now
            });
        }

        await _context.SaveChangesAsync();

        return await GetByTrainerIdAsync(trainerId);
    }

    private async Task<DateTime> ResolveInitialRateValidFromAsync(int trainerId, DateTime fallback)
    {
        var firstSessionDate = await _context.Sessions
            .Where(s => s.TrainerId == trainerId)
            .OrderBy(s => s.StartAt)
            .Select(s => (DateTime?)s.StartAt)
            .FirstOrDefaultAsync();

        return firstSessionDate ?? fallback;
    }

    private static void ValidateRates(UpdateTrainerRatesDto request)
    {
        if (!request.HourlyRate.HasValue && !request.GroupSessionRate.HasValue)
            throw new InvalidOperationException("At least one rate is required.");

        if (request.HourlyRate.HasValue && request.HourlyRate.Value < 0)
            throw new InvalidOperationException("Hourly rate cannot be negative.");
        if (request.GroupSessionRate < 0)
            throw new InvalidOperationException("Group session rate cannot be negative.");
    }
}
