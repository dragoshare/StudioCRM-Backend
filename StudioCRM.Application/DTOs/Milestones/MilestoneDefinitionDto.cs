namespace StudioCRM.Application.DTOs.Milestones;

public class MilestoneDefinitionDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int RequiredMonths { get; set; }
    public string RewardName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsActive { get; set; }
}
