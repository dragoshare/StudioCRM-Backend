namespace StudioCRM.Application.DTOs.TrainingPlans;

public class TrainingPlanFileDownloadDto
{
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = "application/octet-stream";
    public byte[] Content { get; set; } = Array.Empty<byte>();
}
