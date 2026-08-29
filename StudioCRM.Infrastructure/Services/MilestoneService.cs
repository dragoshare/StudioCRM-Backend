using Microsoft.EntityFrameworkCore;
using StudioCRM.Application.DTOs.Milestones;
using StudioCRM.Application.Interfaces;
using StudioCRM.Domain.Entities;
using StudioCRM.Infrastructure.Persistence;

namespace StudioCRM.Infrastructure.Services;

public class MilestoneService : IMilestoneService
{
    private const int MaxNameLength = 150;
    private const int MaxRewardNameLength = 150;
    private const int MaxDescriptionLength = 500;

    private readonly StudioCRMDbContext _context;

    public MilestoneService(StudioCRMDbContext context)
    {
        _context = context;
    }

    public async Task<List<MilestoneDefinitionDto>> GetDefinitionsAsync(bool includeInactive = false)
    {
        var query = _context.MilestoneDefinitions.AsQueryable();

        if (!includeInactive)
            query = query.Where(x => x.IsActive);

        var definitions = await query
            .OrderBy(x => x.RequiredMonths)
            .ThenBy(x => x.Id)
            .ToListAsync();

        return definitions.Select(MapDefinition).ToList();
    }

    public async Task<MilestoneDefinitionDto> CreateDefinitionAsync(UpsertMilestoneDefinitionRequest request)
    {
        var definition = new MilestoneDefinition();
        ApplyDefinitionRequest(definition, request);

        await _context.MilestoneDefinitions.AddAsync(definition);
        await _context.SaveChangesAsync();

        return MapDefinition(definition);
    }

    public async Task<MilestoneDefinitionDto?> UpdateDefinitionAsync(
        int id,
        UpsertMilestoneDefinitionRequest request)
    {
        var definition = await _context.MilestoneDefinitions
            .FirstOrDefaultAsync(x => x.Id == id);

        if (definition is null)
            return null;

        ApplyDefinitionRequest(definition, request);
        await _context.SaveChangesAsync();

        return MapDefinition(definition);
    }

    public async Task<MilestoneDefinitionDto?> SetDefinitionActiveAsync(int id, bool isActive)
    {
        var definition = await _context.MilestoneDefinitions
            .FirstOrDefaultAsync(x => x.Id == id);

        if (definition is null)
            return null;

        definition.IsActive = isActive;
        await _context.SaveChangesAsync();

        return MapDefinition(definition);
    }

    public async Task<ClientMilestonesSummaryDto?> GetClientMilestonesAsync(int clientId)
    {
        var client = await _context.Clients
            .Include(c => c.User)
            .Include(c => c.Milestones)
                .ThenInclude(cm => cm.MilestoneDefinition)
            .Include(c => c.Milestones)
                .ThenInclude(cm => cm.RewardClaimedByUser)
            .Include(c => c.Milestones)
                .ThenInclude(cm => cm.RewardClaimedByTrainer)
                .ThenInclude(t => t!.User)
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
                RewardClaimedByUserId = existing?.RewardClaimedByUserId,
                RewardClaimedByUserName = GetUserFullName(existing?.RewardClaimedByUser),
                RewardClaimedByTrainerId = existing?.RewardClaimedByTrainerId,
                RewardClaimedByTrainerName = existing?.RewardClaimedByTrainer is null
                    ? null
                    : GetUserFullName(existing.RewardClaimedByTrainer.User),
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
            trainerUserId,
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
            ownerUserId,
            null,
            note);
    }

    public async Task<bool> UnclaimRewardAsTrainerAsync(
        int trainerUserId,
        int clientId,
        int milestoneDefinitionId)
    {
        var trainer = await _context.Trainers
            .FirstOrDefaultAsync(t => t.UserId == trainerUserId);

        if (trainer is null)
            return false;

        var clientBelongsToTrainer = await _context.Clients
            .AnyAsync(c => c.Id == clientId && c.TrainerId == trainer.Id);

        if (!clientBelongsToTrainer)
            return false;

        return await UnclaimRewardInternalAsync(clientId, milestoneDefinitionId);
    }

    public async Task<bool> UnclaimRewardAsOwnerAsync(
        int ownerUserId,
        int clientId,
        int milestoneDefinitionId)
    {
        return await UnclaimRewardInternalAsync(clientId, milestoneDefinitionId);
    }

    private async Task<bool> ClaimRewardInternalAsync(
        int clientId,
        int milestoneDefinitionId,
        int claimedByUserId,
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
                RewardClaimedByUserId = claimedByUserId,
                RewardClaimedByTrainerId = trainerId,
                RewardClaimNote = NormalizeOptionalText(note)
            };

            _context.ClientMilestones.Add(existing);
        }
        else
        {
            existing.IsRewardClaimed = true;
            existing.RewardClaimedAt = DateTime.UtcNow;
            existing.RewardClaimedByUserId = claimedByUserId;
            existing.RewardClaimedByTrainerId = trainerId;
            existing.RewardClaimNote = NormalizeOptionalText(note);
        }

        await _context.SaveChangesAsync();

        return true;
    }

    private async Task<bool> UnclaimRewardInternalAsync(
        int clientId,
        int milestoneDefinitionId)
    {
        var milestone = await _context.ClientMilestones
            .FirstOrDefaultAsync(x =>
                x.ClientId == clientId &&
                x.MilestoneDefinitionId == milestoneDefinitionId);

        if (milestone is null || !milestone.IsRewardClaimed)
            return false;

        milestone.IsRewardClaimed = false;
        milestone.RewardClaimedAt = null;
        milestone.RewardClaimedByUserId = null;
        milestone.RewardClaimedByTrainerId = null;
        milestone.RewardClaimNote = null;

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

    private static void ApplyDefinitionRequest(
        MilestoneDefinition definition,
        UpsertMilestoneDefinitionRequest request)
    {
        definition.Name = NormalizeRequiredText(request.Name, "Milestone name", MaxNameLength);
        definition.RequiredMonths = NormalizeRequiredMonths(request.RequiredMonths);
        definition.RewardName = NormalizeRequiredText(request.RewardName, "Reward name", MaxRewardNameLength);
        definition.Description = NormalizeOptionalText(request.Description, MaxDescriptionLength);
        definition.IsActive = request.IsActive;
    }

    private static MilestoneDefinitionDto MapDefinition(MilestoneDefinition definition)
    {
        return new MilestoneDefinitionDto
        {
            Id = definition.Id,
            Name = definition.Name,
            RequiredMonths = definition.RequiredMonths,
            RewardName = definition.RewardName,
            Description = definition.Description,
            IsActive = definition.IsActive
        };
    }

    private static int NormalizeRequiredMonths(int requiredMonths)
    {
        if (requiredMonths <= 0)
            throw new InvalidOperationException("Required months must be greater than zero.");

        return requiredMonths;
    }

    private static string NormalizeRequiredText(string? value, string fieldName, int maxLength)
    {
        var normalized = value?.Trim();

        if (string.IsNullOrWhiteSpace(normalized))
            throw new InvalidOperationException($"{fieldName} is required.");

        if (normalized.Length > maxLength)
            throw new InvalidOperationException($"{fieldName} cannot exceed {maxLength} characters.");

        return normalized;
    }

    private static string? NormalizeOptionalText(string? value, int maxLength = MaxDescriptionLength)
    {
        var normalized = value?.Trim();

        if (string.IsNullOrWhiteSpace(normalized))
            return null;

        if (normalized.Length > maxLength)
            throw new InvalidOperationException($"Text cannot exceed {maxLength} characters.");

        return normalized;
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
            var fullName = GetUserFullName(client.User);

            if (!string.IsNullOrWhiteSpace(fullName))
                return fullName;
        }

        var clientName = $"{client.FirstName} {client.LastName}".Trim();

        if (!string.IsNullOrWhiteSpace(clientName))
            return clientName;

        return $"Klient #{client.Id}";
    }

    private static string? GetUserFullName(User? user)
    {
        if (user is null)
            return null;

        var fullName = $"{user.FirstName} {user.LastName}".Trim();

        return string.IsNullOrWhiteSpace(fullName)
            ? user.Email
            : fullName;
    }
}
