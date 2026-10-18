using System.Text.Json;

namespace StudioCRM.Application.DTOs.Sessions;

public class SessionCorrectionDto
{
    public int Id { get; set; }

    public int? SessionId { get; set; }

    public int OriginalSessionId { get; set; }

    // SessionRecalculated (POST /complete), SessionStatusCorrected (PUT from Completed to Planned/Cancelled),
    // CompletedSessionUpdated (PUT keeping Completed: time/trainer/location/title only).
    public string ChangeType { get; set; } = string.Empty;

    public string? Reason { get; set; }

    // Snapshot (PascalCase keys): Status, StartAt, EndAt, TrainerId, LocationId, PlannedSessionType, ActualSessionType,
    // ActualParticipantsCount, CompletedAt, Participants[]: ClientId, AttendanceStatus, CountsAgainstPackage,
    // IsCountedFromPackage, SessionsCharged, PackageId, ClientPackageId, PlannedBillingType, ActualBillingType,
    // ExpectedUnitPrice, ActualUnitPrice, BalanceDifference.
    public JsonElement BeforeState { get; set; }

    public JsonElement AfterState { get; set; }

    public int? ChangedByUserId { get; set; }

    public string? ChangedByFullName { get; set; }

    public DateTime CreatedAt { get; set; }
}
