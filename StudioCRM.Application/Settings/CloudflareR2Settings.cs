namespace StudioCRM.Application.Settings;

public class CloudflareR2Settings
{
    public string AccountId { get; set; } = string.Empty;
    public string Endpoint { get; set; } = string.Empty;
    public string AccessKeyId { get; set; } = string.Empty;
    public string SecretAccessKey { get; set; } = string.Empty;
    public string BucketName { get; set; } = string.Empty;
    public string PublicBaseUrl { get; set; } = string.Empty;
    public int MaxTrainingPlanFileSizeMb { get; set; } = 20;
}
