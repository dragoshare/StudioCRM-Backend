namespace StudioCRM.Domain.Entities;
public class OutlookOAuthChallenge
{
    public string Id { get; set; } = "";
    public int UserId { get; set; }
    public string CodeVerifier { get; set; } = "";
    public DateTime ExpiresAt { get; set; }
}
