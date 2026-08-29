using StudioCRM.Application.DTOs.Milestones;

namespace StudioCRM.Application.Interfaces;

public interface IMilestoneService
{
    Task<List<MilestoneDefinitionDto>> GetDefinitionsAsync(bool includeInactive = false);

    Task<MilestoneDefinitionDto> CreateDefinitionAsync(UpsertMilestoneDefinitionRequest request);

    Task<MilestoneDefinitionDto?> UpdateDefinitionAsync(int id, UpsertMilestoneDefinitionRequest request);

    Task<MilestoneDefinitionDto?> SetDefinitionActiveAsync(int id, bool isActive);

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

    Task<bool> UnclaimRewardAsTrainerAsync(
        int trainerUserId,
        int clientId,
        int milestoneDefinitionId);

    Task<bool> UnclaimRewardAsOwnerAsync(
        int ownerUserId,
        int clientId,
        int milestoneDefinitionId);
}
