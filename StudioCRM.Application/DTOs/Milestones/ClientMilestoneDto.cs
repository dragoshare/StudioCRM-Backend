namespace StudioCRM.Application.DTOs.Milestones;

public class ClientMilestoneDto
{
    public int MilestoneDefinitionId { get; set; }

    public string Name { get; set; } = string.Empty;

    public int RequiredMonths { get; set; }

    public string RewardName { get; set; } = string.Empty;

    public string? Description { get; set; }

    public bool IsAchieved { get; set; }

    public DateTime? AchievedAt { get; set; }

    public bool IsRewardClaimed { get; set; }

    public DateTime? RewardClaimedAt { get; set; }

    public int? RewardClaimedByUserId { get; set; }

    public string? RewardClaimedByUserName { get; set; }

    public int? RewardClaimedByTrainerId { get; set; }

    public string? RewardClaimedByTrainerName { get; set; }

    public string? RewardClaimNote { get; set; }
}
