namespace StudioCRM.Application.Common;

// A business-rule rejection with a stable code for the frontend (400 { message, code }). It is still an
// InvalidOperationException, so every existing handler keeps returning 400 with the message.
public class CodedInvalidOperationException(string code, string message) : InvalidOperationException(message)
{
    public string Code { get; } = code;

    // Error body used by controllers: { message, code } (code is null for uncoded rejections).
    public static object Body(Exception exception) => new { message = exception.Message, code = CodeOf(exception) };

    public static string? CodeOf(Exception exception) => exception switch
    {
        CodedInvalidOperationException coded => coded.Code,
        // Transitional: SessionParticipantService still throws the paid-settlement rejection uncoded.
        InvalidOperationException { Message: PaidSettlementMessage } => SessionCorrectionErrorCodes.TrainerSettlementPaid,
        _ => null
    };

    public const string PaidSettlementMessage = "Session cannot be changed because the trainer settlement for this month has already been paid.";
}

// Codes of session correction rejections (POST /api/sessions/{id}/complete, PUT /api/sessions/{id}).
public static class SessionCorrectionErrorCodes
{
    public const string TrainerSettlementPaid = "TrainerSettlementPaid";
    public const string ArchivedClientOrClosedPackage = "ArchivedClientOrClosedPackage";
    public const string MultipleRenewalCycles = "MultipleRenewalCycles";
    public const string RenewalClosed = "RenewalClosed";
    public const string RenewalAlreadyUsed = "RenewalAlreadyUsed";
    public const string RenewalPaymentPending = "RenewalPaymentPending";
    public const string OtherActivePackage = "OtherActivePackage";
    public const string CorrectionReasonTooLong = "CorrectionReasonTooLong";
    public const string InvalidAttendanceStatus = "InvalidAttendanceStatus";
}

// Attendance values accepted when an owner settles or corrects a session. Only Present uses an entry
// (with countsAgainstPackage = true); the cancellations are recorded without using an entry.
public static class SessionAttendanceStatuses
{
    public const string Present = "Present";
    public const string CancelledInTime = "CancelledInTime";
    public const string CancelledLate = "CancelledLate";
    public static readonly string[] Settlement = [Present, CancelledInTime, CancelledLate];
}
