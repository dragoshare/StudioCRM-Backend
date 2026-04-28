namespace StudioCRM.Application.Milestones;

public class PendingRewardDto
{
    public int ClientId { get; set; }

    public string ClientFullName { get; set; } = string.Empty;

    public int MilestoneDefinitionId { get; set; }

    public string MilestoneName { get; set; } = string.Empty;

    public string RewardName { get; set; } = string.Empty;

    public DateTime AchievedAt { get; set; }
}