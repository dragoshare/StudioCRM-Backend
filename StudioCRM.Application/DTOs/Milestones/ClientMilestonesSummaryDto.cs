namespace StudioCRM.Application.Milestones;

public class ClientMilestonesSummaryDto
{
    public int ClientId { get; set; }

    public string ClientFullName { get; set; } = string.Empty;

    public DateTime? TrainingStartDate { get; set; }

    public int TrainingDays { get; set; }

    public int TrainingMonths { get; set; }

    public List<ClientMilestoneDto> Milestones { get; set; } = new();
}