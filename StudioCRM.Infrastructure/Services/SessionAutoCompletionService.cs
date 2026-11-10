using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using StudioCRM.Application.DTOs.SessionParticipants;
using StudioCRM.Application.Interfaces;
using StudioCRM.Domain.Entities;
using StudioCRM.Domain.Enums;
using StudioCRM.Infrastructure.Persistence;
using StudioCRM.Application.Common;

namespace StudioCRM.Infrastructure.Services;

public class SessionAutoCompletionService : ISessionAutoCompletionService
{
    private static readonly HashSet<string> FinishedStatuses = new(StringComparer.OrdinalIgnoreCase)
    {
        "Completed",
        "Cancelled"
    };

    private readonly StudioCRMDbContext _context;
    private readonly ISessionParticipantService _sessionParticipantService;
    private readonly ILogger<SessionAutoCompletionService> _logger;

    public SessionAutoCompletionService(
        StudioCRMDbContext context,
        ISessionParticipantService sessionParticipantService,
        ILogger<SessionAutoCompletionService> logger)
    {
        _context = context;
        _sessionParticipantService = sessionParticipantService;
        _logger = logger;
    }

    public async Task<SessionAutoCompletionResult> CompleteFinishedSessionsAsync(
        CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var result = new SessionAutoCompletionResult();

        // A session that failed recently waits before the next attempt and goes after the others,
        // so a few sessions that keep failing cannot hold up the whole batch.
        var retryFailedBefore = now - FailedRetryDelay;
        var sessionIds = await _context.Sessions
            .AsNoTracking()
            .Where(s =>
                !s.IsDeleted &&
                s.EndAt <= now &&
                !FinishedStatuses.Contains(s.Status) &&
                (s.AutoCompletionFailedAt == null || s.AutoCompletionFailedAt <= retryFailedBefore))
            .OrderBy(s => s.AutoCompletionFailedAt != null)
            .ThenBy(s => s.EndAt)
            .Select(s => s.Id)
            .Take(50)
            .ToListAsync(cancellationToken);

        foreach (var sessionId in sessionIds)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var (request, skipReason) = await BuildCompletionRequestAsync(sessionId, cancellationToken);

            if (request is null)
            {
                result.SkippedCount++;
                if (skipReason != null) await RecordFailureAsync(sessionId, skipReason, now, cancellationToken);
                continue;
            }

            try
            {
                var completed = await _sessionParticipantService.CompleteSessionAutomaticallyAsync(sessionId, request);

                if (completed)
                    result.CompletedCount++;
                else
                    result.SkippedCount++;
            }
            catch (Exception ex)
            {
                result.FailedCount++;
                _logger.LogWarning(ex, "Automatic session completion failed for session {SessionId}.", sessionId);
                // Business rejections already carry a user-facing reason; anything else is a technical error.
                await RecordFailureAsync(sessionId, ex is InvalidOperationException
                    ? ex.Message
                    : "Błąd techniczny automatycznego rozliczenia. Spróbuj zakończyć sesję ręcznie albo zgłoś problem.", now, cancellationToken);
            }
        }

        return result;
    }

    private static readonly TimeSpan FailedRetryDelay = TimeSpan.FromMinutes(15);

    // Stores the reason on the session (shown to the owner and the trainer as an alert). Never fails the batch.
    private async Task RecordFailureAsync(int sessionId, string reason, DateTime now, CancellationToken cancellationToken)
    {
        try
        {
            _context.ChangeTracker.Clear();
            var session = await _context.Sessions.FirstOrDefaultAsync(s => s.Id == sessionId, cancellationToken);
            if (session is null || FinishedStatuses.Contains(session.Status)) return;
            session.AutoCompletionFailedAt = now;
            session.AutoCompletionError = reason.Length > 1000 ? reason[..1000] : reason;
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Could not record the automatic completion failure of session {SessionId}.", sessionId);
        }
        finally
        {
            _context.ChangeTracker.Clear();
        }
    }

    private async Task<(CompleteSessionDto? Request, string? SkipReason)> BuildCompletionRequestAsync(
        int sessionId,
        CancellationToken cancellationToken)
    {
        var session = await _context.Sessions
            .AsNoTracking()
            .Include(s => s.Participants)
            .FirstOrDefaultAsync(s => s.Id == sessionId, cancellationToken);

        if (session is null || FinishedStatuses.Contains(session.Status))
            return (null, null);

        if (ConsultationSessions.IsConsultation(session.PlannedSessionType))
            return (new CompleteSessionDto { ActualSessionType = ConsultationSessions.SessionType }, null);

        var billableParticipants = session.Participants
            .Where(IsAutoCompletableParticipant)
            .ToList();

        // A group class that took place is completed even with nobody enrolled in CRM (trainer paid the group rate).
        if (billableParticipants.Count == 0 && !GroupBookingPolicy.IsGroup(session))
            return (null, "Sesja nie ma obecnych uczestników. Dodaj uczestników i zakończ ją ręcznie albo ją odwołaj.");

        var actualSessionType = GroupBookingPolicy.IsGroup(session)
            ? SessionBillingType.Group : ResolveActualSessionType(billableParticipants.Count);

        if (actualSessionType is null)
        {
            _logger.LogWarning(
                "Session {SessionId} has {ParticipantCount} participants and cannot be auto-completed into billing type.",
                sessionId,
                billableParticipants.Count);

            return (null, $"Sesja ma {billableParticipants.Count} uczestników, więc nie da się automatycznie ustalić typu treningu (1–4 osoby). Zakończ ją ręcznie.");
        }

        return (new CompleteSessionDto
        {
            ActualSessionType = actualSessionType.Value.ToString(),
            Participants = session.Participants
                .Select(MapParticipant)
                .ToList()
        }, null);
    }

    private static bool IsAutoCompletableParticipant(SessionParticipant participant)
    {
        return participant.AttendanceStatus is "Planned" or "Present";
    }

    private static CompleteSessionParticipantDto MapParticipant(SessionParticipant participant)
    {
        var attendanceStatus = participant.AttendanceStatus == "Planned"
            ? "Present"
            : participant.AttendanceStatus;

        return new CompleteSessionParticipantDto
        {
            ClientId = participant.ClientId,
            AttendanceStatus = attendanceStatus,
            CountsAgainstPackage = participant.CountsAgainstPackage,
            SessionsCharged = Math.Max(1, participant.SessionsCharged),
            Note = participant.Note
        };
    }

    private static SessionBillingType? ResolveActualSessionType(int participantCount)
    {
        return participantCount switch
        {
            1 => SessionBillingType.OneToOne,
            2 => SessionBillingType.TwoToOne,
            3 => SessionBillingType.ThreeToOne,
            4 => SessionBillingType.FourToOne,
            _ => null
        };
    }
}
