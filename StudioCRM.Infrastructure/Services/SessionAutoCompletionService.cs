using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using StudioCRM.Application.DTOs.SessionParticipants;
using StudioCRM.Application.Interfaces;
using StudioCRM.Domain.Entities;
using StudioCRM.Domain.Enums;
using StudioCRM.Infrastructure.Persistence;

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

        var sessionIds = await _context.Sessions
            .AsNoTracking()
            .Where(s =>
                !s.IsDeleted &&
                s.EndAt <= now &&
                !FinishedStatuses.Contains(s.Status))
            .OrderBy(s => s.EndAt)
            .Select(s => s.Id)
            .Take(50)
            .ToListAsync(cancellationToken);

        foreach (var sessionId in sessionIds)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var request = await BuildCompletionRequestAsync(sessionId, cancellationToken);

            if (request is null)
            {
                result.SkippedCount++;
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
            }
        }

        return result;
    }

    private async Task<CompleteSessionDto?> BuildCompletionRequestAsync(
        int sessionId,
        CancellationToken cancellationToken)
    {
        var session = await _context.Sessions
            .AsNoTracking()
            .Include(s => s.Participants)
            .FirstOrDefaultAsync(s => s.Id == sessionId, cancellationToken);

        if (session is null || FinishedStatuses.Contains(session.Status))
            return null;

        var billableParticipants = session.Participants
            .Where(IsAutoCompletableParticipant)
            .ToList();

        if (billableParticipants.Count == 0)
            return null;

        var actualSessionType = ResolveActualSessionType(billableParticipants.Count);

        if (actualSessionType is null)
        {
            _logger.LogWarning(
                "Session {SessionId} has {ParticipantCount} participants and cannot be auto-completed into billing type.",
                sessionId,
                billableParticipants.Count);

            return null;
        }

        return new CompleteSessionDto
        {
            ActualSessionType = actualSessionType.Value.ToString(),
            Participants = session.Participants
                .Select(MapParticipant)
                .ToList()
        };
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
            _ => SessionBillingType.Group
        };
    }
}
