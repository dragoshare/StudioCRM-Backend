using Microsoft.EntityFrameworkCore;
using StudioCRM.Application.Interfaces;
using StudioCRM.Application.Milestones;
using StudioCRM.Domain.Entities;
using StudioCRM.Infrastructure.Persistence;

namespace StudioCRM.Infrastructure.Services;

public class MilestoneService : IMilestoneService
{
    private readonly StudioCRMDbContext _context;

    public MilestoneService(StudioCRMDbContext context)
    {
        _context = context;
    }

    public async Task<ClientMilestonesSummaryDto?> GetClientMilestonesAsync(int clientId)
    {
        var client = await _context.Clients
            .Include(c => c.User)
            .Include(c => c.Milestones)
                .ThenInclude(cm => cm.MilestoneDefinition)
            .FirstOrDefaultAsync(c => c.Id == clientId);

        if (client is null)
            return null;

        var definitions = await _context.MilestoneDefinitions
            .Where(x => x.IsActive)
            .OrderBy(x => x.RequiredMonths)
            .ToListAsync();

        var now = DateTime.UtcNow.Date;

        var trainingDays = client.TrainingStartDate.HasValue
            ? Math.Max(0, (now - client.TrainingStartDate.Value.Date).Days)
            : 0;

        var trainingMonths = client.TrainingStartDate.HasValue
            ? CalculateFullMonths(client.TrainingStartDate.Value.Date, now)
            : 0;

        var result = new ClientMilestonesSummaryDto
        {
            ClientId = client.Id,
            ClientFullName = GetClientFullName(client),
            TrainingStartDate = client.TrainingStartDate,
            TrainingDays = trainingDays,
            TrainingMonths = trainingMonths
        };

        foreach (var definition in definitions)
        {
            var achievedAt = client.TrainingStartDate?.Date.AddMonths(definition.RequiredMonths);
            var isAchieved = client.TrainingStartDate.HasValue && achievedAt <= now;

            var existing = client.Milestones
                .FirstOrDefault(x => x.MilestoneDefinitionId == definition.Id);

            result.Milestones.Add(new ClientMilestoneDto
            {
                MilestoneDefinitionId = definition.Id,
                Name = definition.Name,
                RequiredMonths = definition.RequiredMonths,
                RewardName = definition.RewardName,
                Description = definition.Description,
                IsAchieved = isAchieved,
                AchievedAt = isAchieved ? existing?.AchievedAt ?? achievedAt : null,
                IsRewardClaimed = existing?.IsRewardClaimed ?? false,
                RewardClaimedAt = existing?.RewardClaimedAt,
                RewardClaimNote = existing?.RewardClaimNote
            });
        }

        return result;
    }

    public async Task<List<PendingRewardDto>> GetPendingRewardsForTrainerAsync(int trainerUserId)
    {
        var trainer = await _context.Trainers
            .FirstOrDefaultAsync(t => t.UserId == trainerUserId);

        if (trainer is null)
            return new List<PendingRewardDto>();

        var clients = await _context.Clients
            .Include(c => c.User)
            .Include(c => c.Milestones)
            .Where(c => c.TrainerId == trainer.Id)
            .ToListAsync();

        return await BuildPendingRewardsAsync(clients);
    }

    public async Task<List<PendingRewardDto>> GetPendingRewardsForOwnerAsync()
    {
        var clients = await _context.Clients
            .Include(c => c.User)
            .Include(c => c.Milestones)
            .ToListAsync();

        return await BuildPendingRewardsAsync(clients);
    }

    public async Task<bool> TrainerHasAccessToClientAsync(int trainerUserId, int clientId)
    {
        var trainer = await _context.Trainers
            .FirstOrDefaultAsync(t => t.UserId == trainerUserId);

        if (trainer is null)
            return false;

        return await _context.Clients
            .AnyAsync(c => c.Id == clientId && c.TrainerId == trainer.Id);
    }

    public async Task<bool> ClaimRewardAsTrainerAsync(
        int trainerUserId,
        int clientId,
        int milestoneDefinitionId,
        string? note)
    {
        var trainer = await _context.Trainers
            .FirstOrDefaultAsync(t => t.UserId == trainerUserId);

        if (trainer is null)
            return false;

        var clientBelongsToTrainer = await _context.Clients
            .AnyAsync(c => c.Id == clientId && c.TrainerId == trainer.Id);

        if (!clientBelongsToTrainer)
            return false;

        return await ClaimRewardInternalAsync(
            clientId,
            milestoneDefinitionId,
            trainer.Id,
            note);
    }

    public async Task<bool> ClaimRewardAsOwnerAsync(
        int ownerUserId,
        int clientId,
        int milestoneDefinitionId,
        string? note)
    {
        return await ClaimRewardInternalAsync(
            clientId,
            milestoneDefinitionId,
            null,
            note);
    }

    private async Task<bool> ClaimRewardInternalAsync(
        int clientId,
        int milestoneDefinitionId,
        int? trainerId,
        string? note)
    {
        var client = await _context.Clients
            .Include(c => c.Milestones)
            .FirstOrDefaultAsync(c => c.Id == clientId);

        if (client is null || client.TrainingStartDate is null)
            return false;

        var definition = await _context.MilestoneDefinitions
            .FirstOrDefaultAsync(x => x.Id == milestoneDefinitionId && x.IsActive);

        if (definition is null)
            return false;

        var achievedAt = client.TrainingStartDate.Value.Date.AddMonths(definition.RequiredMonths);

        if (achievedAt > DateTime.UtcNow.Date)
            return false;

        var existing = client.Milestones
            .FirstOrDefault(x => x.MilestoneDefinitionId == milestoneDefinitionId);

        if (existing is null)
        {
            existing = new ClientMilestone
            {
                ClientId = clientId,
                MilestoneDefinitionId = milestoneDefinitionId,
                AchievedAt = achievedAt,
                IsRewardClaimed = true,
                RewardClaimedAt = DateTime.UtcNow,
                RewardClaimedByTrainerId = trainerId,
                RewardClaimNote = note
            };

            _context.ClientMilestones.Add(existing);
        }
        else
        {
            existing.IsRewardClaimed = true;
            existing.RewardClaimedAt = DateTime.UtcNow;
            existing.RewardClaimedByTrainerId = trainerId;
            existing.RewardClaimNote = note;
        }

        await _context.SaveChangesAsync();

        return true;
    }

    private async Task<List<PendingRewardDto>> BuildPendingRewardsAsync(List<Client> clients)
    {
        var definitions = await _context.MilestoneDefinitions
            .Where(x => x.IsActive)
            .OrderBy(x => x.RequiredMonths)
            .ToListAsync();

        var now = DateTime.UtcNow.Date;
        var result = new List<PendingRewardDto>();

        foreach (var client in clients)
        {
            if (client.TrainingStartDate is null)
                continue;

            foreach (var definition in definitions)
            {
                var achievedAt = client.TrainingStartDate.Value.Date.AddMonths(definition.RequiredMonths);

                if (achievedAt > now)
                    continue;

                var existing = client.Milestones
                    .FirstOrDefault(x => x.MilestoneDefinitionId == definition.Id);

                if (existing?.IsRewardClaimed == true)
                    continue;

                result.Add(new PendingRewardDto
                {
                    ClientId = client.Id,
                    ClientFullName = GetClientFullName(client),
                    MilestoneDefinitionId = definition.Id,
                    MilestoneName = definition.Name,
                    RewardName = definition.RewardName,
                    AchievedAt = existing?.AchievedAt ?? achievedAt
                });
            }
        }

        return result
            .OrderBy(x => x.AchievedAt)
            .ThenBy(x => x.ClientFullName)
            .ToList();
    }

    private static int CalculateFullMonths(DateTime startDate, DateTime endDate)
    {
        var months = ((endDate.Year - startDate.Year) * 12) + endDate.Month - startDate.Month;

        if (endDate.Day < startDate.Day)
            months--;

        return Math.Max(0, months);
    }

    private static string GetClientFullName(Client client)
    {
        if (client.User is not null)
        {
            var firstName = client.User.FirstName ?? string.Empty;
            var lastName = client.User.LastName ?? string.Empty;

            var fullName = $"{firstName} {lastName}".Trim();

            if (!string.IsNullOrWhiteSpace(fullName))
                return fullName;
        }

        return $"Klient #{client.Id}";
    }
}