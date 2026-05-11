namespace StudioCRM.Application.DTOs.TrainingPlans;

public class TrainingPlanDto
{
    public int ClientId { get; set; }
    public string? GoogleDriveFolderId { get; set; }
    public string? GoogleDriveFolderUrl { get; set; }
    public string? FileId { get; set; }
    public string? FileName { get; set; }
    public string? Url { get; set; }
}

public class UpdateTrainingPlanRequest
{
    public string? GoogleDriveFolderId { get; set; }
    public string? FileId { get; set; }
    public string? FileName { get; set; }
    public string? Url { get; set; }
}
