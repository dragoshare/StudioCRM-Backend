using StudioCRM.Application.DTOs.Milestones;

namespace StudioCRM.Application.Interfaces;

public interface IMilestoneService
{
    Task<ClientMilestonesSummaryDto?> GetClientMilestonesAsync(int clientId);

    Task<List<PendingRewardDto>> GetPendingRewardsForTrainerAsync(int trainerUserId);

    Task<List<PendingRewardDto>> GetPendingRewardsForOwnerAsync();

    Task<bool> TrainerHasAccessToClientAsync(int trainerUserId, int clientId);

    Task<bool> ClaimRewardAsTrainerAsync(
        int trainerUserId,
        int clientId,
        int milestoneDefinitionId,
        string? note);

    Task<bool> ClaimRewardAsOwnerAsync(
        int ownerUserId,
        int clientId,
        int milestoneDefinitionId,
        string? note);
}