namespace StudioCRM.Application.DTOs.Clients;

public class ClientEmailChangeDto
{
    public int Id { get; set; }
    public int ClientId { get; set; }
    public string CurrentEmail { get; set; } = "";
    public string RequestedEmail { get; set; } = "";
    public string Status { get; set; } = "";
    public DateTime CreatedAt { get; set; }
    public DateTime? VerificationExpiresAt { get; set; }
    public string? ReviewReason { get; set; }
}

public class ReviewClientEmailChangeRequest
{
    public bool Approve { get; set; }
    public string? Reason { get; set; }
}

public class VerifyClientEmailChangeRequest
{
    public int RequestId { get; set; }
    public string Token { get; set; } = "";
}
