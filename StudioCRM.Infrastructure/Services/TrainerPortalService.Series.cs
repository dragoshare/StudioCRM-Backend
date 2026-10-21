using Microsoft.EntityFrameworkCore;
using StudioCRM.Application.Common;
using StudioCRM.Application.DTOs.Sessions;

namespace StudioCRM.Infrastructure.Services;

// Session series created by a trainer in the trainer portal: always the logged-in trainer, only own locations and
// clients, individual/semi sessions only (group classes and public booking stay with the owner).
public partial class TrainerPortalService
{
    internal const string TrainerSeriesGroupMessage =
        "Trainers can create series of individual or semi sessions only; group classes and public sessions are managed by the owner.";

    public async Task<SessionSeriesDto> CreateSessionSeriesAsync(CreateSessionSeriesDto request)
    {
        if (request.Session is null || request.Recurrence is null)
            throw new InvalidOperationException("Session and recurrence settings are required.");

        var trainerId = await GetCurrentTrainerIdAsync()
            ?? throw new InvalidOperationException("Trainer not found.");

        var session = request.Session;
        // The Outlook category would turn the series into group classes on the next Outlook update.
        if (session.IsPubliclyBookable || GroupClassSessions.IsGroup(session.PlannedSessionType) ||
            (session.OutlookCategories?.Any(GroupClassSessions.IsOutlookCategory) ?? false))
            throw new InvalidOperationException(TrainerSeriesGroupMessage);

        await ValidateTrainerCanManageSessionAsync(trainerId, session.LocationId, session.Participants, session.PlannedSessionType);

        session.TrainerId = trainerId;
        return await _sessionService.CreateSeriesAsync(request);
    }

    public async Task<SessionSeriesOutlookSyncDto?> SyncSessionSeriesToOutlookAsync(string recurringGroupId, SessionRecurrenceDto? recurrence)
    {
        var series = await GetOwnSeriesAsync(recurringGroupId);
        if (series is null)
            return null;

        return await _sessionService.SyncSeriesToOutlookAsync(recurringGroupId, recurrence);
    }

    // Removes only future, not completed and not accounted sessions; past, completed or settled ones stay.
    public async Task<DeleteSessionSeriesResultDto?> DeleteSessionSeriesAsync(string recurringGroupId)
    {
        var series = await GetOwnSeriesAsync(recurringGroupId);
        if (series is null)
            return null;

        if (series.Any(s => s.IsGroup))
            throw new InvalidOperationException(TrainerSeriesGroupMessage);

        var now = DateTime.UtcNow;
        var removable = series
            .Where(s => s.StartAt > now && !s.IsAccounted &&
                !string.Equals(s.Status, "Completed", StringComparison.OrdinalIgnoreCase))
            .ToList();

        // Whole series still ahead: delete it with its Outlook series master in one go.
        if (removable.Count == series.Count)
            return await _sessionService.DeleteSeriesAsync(recurringGroupId);

        // Otherwise delete occurrence by occurrence; each removes its own Outlook occurrence.
        foreach (var session in removable)
            await _sessionService.DeleteAsync(session.Id);

        return new DeleteSessionSeriesResultDto
        {
            RecurringGroupId = recurringGroupId,
            DeletedSessionsCount = removable.Count,
            KeptSessionsCount = series.Count - removable.Count,
            OutlookSeriesDeleted = false
        };
    }

    // null when the series does not exist or is not entirely the current trainer's.
    private async Task<List<SeriesSession>?> GetOwnSeriesAsync(string recurringGroupId)
    {
        if (string.IsNullOrWhiteSpace(recurringGroupId))
            throw new InvalidOperationException("Recurring group id is required.");

        var trainerId = await GetCurrentTrainerIdAsync();
        if (trainerId is null)
            return null;

        var series = await _context.Sessions.AsNoTracking()
            .Where(s => s.RecurringGroupId == recurringGroupId && s.IsRecurring)
            .Select(s => new SeriesSession(
                s.Id,
                s.TrainerId,
                s.StartAt,
                s.Status,
                s.IsPubliclyBookable || s.PlannedSessionType == GroupClassSessions.SessionType || s.ActualSessionType == GroupClassSessions.SessionType,
                s.Participants.Any(p => p.IsCountedFromPackage)))
            .ToListAsync();

        return series.Count == 0 || series.Any(s => s.TrainerId != trainerId.Value) ? null : series;
    }

    private sealed record SeriesSession(int Id, int TrainerId, DateTime StartAt, string Status, bool IsGroup, bool IsAccounted);
}
