namespace StudioCRM.Domain.Entities;

public class ClientMilestone
{
    public int Id { get; set; }

    public int ClientId { get; set; }
    public Client Client { get; set; } = null!;

    public int MilestoneDefinitionId { get; set; }
    public MilestoneDefinition MilestoneDefinition { get; set; } = null!;

    public DateTime AchievedAt { get; set; }

    public bool IsRewardClaimed { get; set; }

    public DateTime? RewardClaimedAt { get; set; }

    public int? RewardClaimedByUserId { get; set; }
    public User? RewardClaimedByUser { get; set; }

    public int? RewardClaimedByTrainerId { get; set; }
    public Trainer? RewardClaimedByTrainer { get; set; }

    public string? RewardClaimNote { get; set; }
}
