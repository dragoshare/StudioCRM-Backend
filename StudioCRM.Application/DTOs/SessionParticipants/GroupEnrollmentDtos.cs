namespace StudioCRM.Application.DTOs.SessionParticipants;
public class PrepareGroupEnrollmentRequest
{
    public int ClientId { get; set; }
    public int PackageId { get; set; }
    public int? ClientPackageId { get; set; }
}
public record GroupEnrollmentDto(int Id, int SessionId, int ClientId, int ClientPackageId,
    string Status, decimal AmountDue, bool ReservesSeat)
{
    // Filled by GET /api/group-enrollments/sessions/{id} (list); null in single-item responses.
    public string? ClientName { get; init; }
    public string? ClientEmail { get; init; }
    public string? ClientPhone { get; init; }
    public bool? ClientIsArchived { get; init; }
    public string? PackageName { get; init; }
    public decimal? PackageTotalPrice { get; init; }
    public decimal? PackageAmountPaid { get; init; }
    public string? PackagePaymentStatus { get; init; }
    public DateTime? PackageValidUntil { get; init; }
    public bool? PackageIsActive { get; init; }
    public int? PackageTotalEntries { get; init; }
    public int? PackageUsedEntries { get; init; }
    // Latest session participant row of this client on the class list (any attendance status).
    public int? SessionParticipantId { get; init; }
    public string? AttendanceStatus { get; init; }
    // Confirm: POST /api/group-enrollments/{id}/confirm. RecordPayment: POST /api/billing/payments with clientPackageId.
    public List<string>? AllowedActions { get; init; }
}
