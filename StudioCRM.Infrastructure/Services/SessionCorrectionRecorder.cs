using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using StudioCRM.Domain.Entities;
using StudioCRM.Infrastructure.Persistence;

namespace StudioCRM.Infrastructure.Services;

internal static class SessionCorrectionRecorder
{
    public static async Task<string> CaptureAsync(StudioCRMDbContext context, int sessionId)
    {
        var snapshot = await context.Sessions
            .IgnoreQueryFilters()
            .Where(x => x.Id == sessionId)
            .Select(x => new
            {
                x.Status,
                x.StartAt,
                x.EndAt,
                x.TrainerId,
                x.LocationId,
                x.PlannedSessionType,
                x.ActualSessionType,
                x.ActualParticipantsCount,
                x.CompletedAt,
                Participants = x.Participants
                    .OrderBy(participant => participant.ClientId)
                    .Select(participant => new
                    {
                        participant.ClientId,
                        participant.AttendanceStatus,
                        participant.CountsAgainstPackage,
                        participant.IsCountedFromPackage,
                        participant.SessionsCharged,
                        participant.PackageId,
                        participant.ClientPackageId,
                        participant.PlannedBillingType,
                        participant.ActualBillingType,
                        participant.ExpectedUnitPrice,
                        participant.ActualUnitPrice,
                        participant.BalanceDifference
                    })
                    .ToList()
            })
            .FirstOrDefaultAsync();

        return JsonSerializer.Serialize(snapshot ?? throw new InvalidOperationException("Session does not exist."));
    }

    public static async Task RecordAsync(
        StudioCRMDbContext context,
        int sessionId,
        string changeType,
        string? reason,
        string beforeStateJson,
        int? changedByUserId)
    {
        await context.SessionCorrections.AddAsync(new SessionCorrection
        {
            SessionId = sessionId,
            OriginalSessionId = sessionId,
            ChangeType = changeType,
            Reason = NormalizeReason(reason),
            BeforeStateJson = beforeStateJson,
            AfterStateJson = await CaptureAsync(context, sessionId),
            ChangedByUserId = changedByUserId,
            CreatedAt = DateTime.UtcNow
        });
    }

    private static string? NormalizeReason(string? reason)
    {
        var normalized = reason?.Trim();
        if (string.IsNullOrWhiteSpace(normalized))
            return null;
        if (normalized.Length > 500)
            throw new InvalidOperationException("Correction reason cannot exceed 500 characters.");
        return normalized;
    }
}
