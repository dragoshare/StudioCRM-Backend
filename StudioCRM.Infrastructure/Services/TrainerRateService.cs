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

        foreach (var oldRate in activeRates)
        {
            oldRate.IsActive = false;
            oldRate.ValidTo = now;
            oldRate.UpdatedAt = now;
        }

        var ratesToCreate = request.HourlyRate.HasValue
            ? new List<UpsertTrainerRateDto>
            {
                new()
                {
                    SessionType = "Hourly",
                    Rate = request.HourlyRate.Value
                }
            }
            : request.Rates;

        foreach (var rate in ratesToCreate)
        {
            var newRate = new TrainerRate
            {
                TrainerId = trainerId,
                SessionType = rate.SessionType,
                Rate = rate.Rate,
                ValidFrom = now,
                IsActive = true,
                CreatedAt = now,
                UpdatedAt = now
            };

            await _context.TrainerRates.AddAsync(newRate);
        }

        await _context.SaveChangesAsync();

        return await GetByTrainerIdAsync(trainerId);
    }

    private static void ValidateRates(UpdateTrainerRatesDto request)
    {
        var allowedTypes = new[]
        {
            "Hourly",
            "OneToOne",
            "TwoToOne",
            "ThreeToOne",
            "FourToOne"
        };

        if (!request.HourlyRate.HasValue && (request.Rates is null || request.Rates.Count == 0))
            throw new InvalidOperationException("At least one rate is required.");

        if (request.HourlyRate.HasValue && request.HourlyRate.Value < 0)
            throw new InvalidOperationException("Hourly rate cannot be negative.");

        if (request.HourlyRate.HasValue)
            return;

        var duplicatedTypes = request.Rates
            .GroupBy(r => r.SessionType)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToList();

        if (duplicatedTypes.Any())
            throw new InvalidOperationException("Duplicated session type in rates.");

        foreach (var rate in request.Rates)
        {
            if (!allowedTypes.Contains(rate.SessionType))
                throw new InvalidOperationException($"Invalid session type: {rate.SessionType}");

            if (rate.Rate < 0)
                throw new InvalidOperationException("Rate cannot be negative.");
        }
    }
}
