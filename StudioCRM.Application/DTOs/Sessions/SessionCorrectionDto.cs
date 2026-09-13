using System.Text.Json;

namespace StudioCRM.Application.DTOs.Sessions;

public class SessionCorrectionDto
{
    public int Id { get; set; }

    public int? SessionId { get; set; }

    public int OriginalSessionId { get; set; }

    public string ChangeType { get; set; } = string.Empty;

    public string? Reason { get; set; }

    public JsonElement BeforeState { get; set; }

    public JsonElement AfterState { get; set; }

    public int? ChangedByUserId { get; set; }

    public DateTime CreatedAt { get; set; }
}
