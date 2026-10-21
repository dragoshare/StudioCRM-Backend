using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using StudioCRM.Application.Common;
using StudioCRM.Application.DTOs.Calendar;
using StudioCRM.Application.DTOs.Invitations;
using StudioCRM.Application.Interfaces;
using StudioCRM.Application.Interfaces.Calendar;
using StudioCRM.Domain.Entities;
using StudioCRM.Domain.Enums;
using StudioCRM.Infrastructure.Persistence;

namespace StudioCRM.Infrastructure.Services.Calendar;

public class ExternalCalendarEventService : IExternalCalendarEventService
{
    private readonly StudioCRMDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly IInvitationService _invitationService;

    public ExternalCalendarEventService(
        StudioCRMDbContext context,
        ICurrentUserService currentUser,
        IInvitationService invitationService)
    {
        _context = context;
        _currentUser = currentUser;
        _invitationService = invitationService;
    }

    public async Task<List<ExternalCalendarEventDto>> GetImportedEventsAsync()
    {
        if (!_currentUser.UserId.HasValue)
            return new List<ExternalCalendarEventDto>();

        var events = await _context.ExternalCalendarEvents
            .Include(x => x.CalendarIntegration)
            .Where(x => x.CalendarIntegration.UserId == _currentUser.UserId.Value)
            .OrderByDescending(x => x.StartAt)
            .ToListAsync();

        return events.Select(x => new ExternalCalendarEventDto
        {
            Id = x.Id,
            Subject = x.Subject,
            BodyPreview = x.BodyPreview,
            StartAt = x.StartAt,
            EndAt = x.EndAt,
            LocationName = x.LocationName,
            LocationEmail = x.LocationEmail,
            OrganizerEmail = x.OrganizerEmail,
            IsConvertedToSession = x.IsConvertedToSession,
            SessionId = x.SessionId,
            IsRecurring = x.IsRecurring,
            SeriesMasterId = x.SeriesMasterId,
            ImportedAt = x.ImportedAt,
            Categories = ReadStringList(x.CategoriesJson),
            CategoryColors = ReadOutlookCategoryColors(x.CategoryColorsJson),
            Warnings = ReadWarnings(x.MappingWarningsJson)
        }).ToList();
    }

    public async Task<List<OutlookMappingIssueDto>> GetIssuesAsync()
    {
        if (!_currentUser.UserId.HasValue)
            return new List<OutlookMappingIssueDto>();

        var userId = _currentUser.UserId.Value;
        var isOwner = _currentUser.IsOwner;
        // Own calendar: every issue. Owner: also the "review in CRM" issues of every trainer's calendar.
        var events = await _context.ExternalCalendarEvents
            .Include(x => x.CalendarIntegration)
                .ThenInclude(i => i.User)
            .Where(x =>
                (x.CalendarIntegration.UserId == userId ||
                 (isOwner && x.MappingWarningsJson!.Contains(OutlookReviewIssues.Prefix))) &&
                x.MappingWarningsJson != null)
            .OrderByDescending(x => x.StartAt)
            .ToListAsync();

        var result = new List<OutlookMappingIssueDto>();

        foreach (var calendarEvent in events)
        {
            var ownCalendar = calendarEvent.CalendarIntegration.UserId == userId;
            var warnings = ReadWarnings(calendarEvent.MappingWarningsJson)
                .Where(warning => ownCalendar || OutlookReviewIssues.IsReview(warning));

            foreach (var warning in warnings)
            {
                result.Add(new OutlookMappingIssueDto
                {
                    ExternalCalendarEventId = calendarEvent.Id,
                    SessionId = calendarEvent.SessionId,
                    Subject = calendarEvent.Subject,
                    StartAt = calendarEvent.StartAt,
                    EndAt = calendarEvent.EndAt,
                    LocationName = calendarEvent.LocationName,
                    OrganizerEmail = calendarEvent.OrganizerEmail,
                    IssueType = ResolveIssueType(warning),
                    RequiresDecision = OutlookReviewIssues.IsReview(warning),
                    CalendarOwnerFullName = calendarEvent.CalendarIntegration.User == null ? null
                        : $"{calendarEvent.CalendarIntegration.User.FirstName} {calendarEvent.CalendarIntegration.User.LastName}".Trim(),
                    Message = warning,
                    Email = ExtractEmailFromWarning(warning),
                    ImportedAt = calendarEvent.ImportedAt
                });
            }
        }

        return result;
    }

    public async Task<int> ConvertToSessionAsync(
        int importedEventId,
        ConvertExternalEventToSessionDto request)
    {
        if (!_currentUser.UserId.HasValue)
            throw new InvalidOperationException("User is not authenticated.");

        // The owner may convert any trainer's event (past events wait for this confirmation).
        var importedEvent = await _context.ExternalCalendarEvents
            .Include(x => x.CalendarIntegration)
            .Include(x => x.Session)
            .FirstOrDefaultAsync(x =>
                x.Id == importedEventId &&
                (_currentUser.IsOwner || x.CalendarIntegration.UserId == _currentUser.UserId.Value));

        if (importedEvent is null)
            throw new InvalidOperationException("Imported event does not exist.");

        var mapper = new OutlookEventMapperService(_context);
        var result = await mapper.MapToSessionAsync(importedEvent);

        if (result.Session is null)
        {
            var message = result.Warnings.Count > 0
                ? string.Join(" | ", result.Warnings)
                : "Could not convert Outlook event to session.";

            throw new InvalidOperationException(message);
        }

        OutlookReviewIssues.RemovePastEventReview(importedEvent);
        await _context.SaveChangesAsync();
        return result.Session.Id;
    }

    public async Task SendInviteFromIssueAsync(string email)
    {
        if (string.IsNullOrWhiteSpace(email))
            throw new InvalidOperationException("Email is required.");

        var normalizedEmail = email.Trim().ToLowerInvariant();

        var existingClient = await _context.Clients
            .AnyAsync(c => c.Email.ToLower() == normalizedEmail && !c.IsDeleted);

        if (existingClient)
            throw new InvalidOperationException("Client already exists.");

        var existingUser = await _context.Users
            .AnyAsync(u => u.Email.ToLower() == normalizedEmail);

        if (existingUser)
            throw new InvalidOperationException("User with this email already exists.");

        var existingInvitation = await _context.Invitations
            .AnyAsync(i => i.Email.ToLower() == normalizedEmail);

        if (existingInvitation)
            throw new InvalidOperationException("Invitation already exists for this email.");

        var request = new CreateInvitationDto
        {
            Email = normalizedEmail,
            Role = "Client"
        };

        await _invitationService.CreateAsync(request);
    }

    public async Task LinkClientFromIssueAsync(int clientId, string email)
    {
        if (string.IsNullOrWhiteSpace(email))
            throw new InvalidOperationException("Email is required.");

        var normalizedEmail = email.Trim().ToLowerInvariant();

        var client = await _context.Clients
            .FirstOrDefaultAsync(c => c.Id == clientId && !c.IsDeleted &&
                (_currentUser.IsOwner || c.Trainer != null && c.Trainer.UserId == _currentUser.UserId));

        if (client is null)
            throw new InvalidOperationException("Client not found.");

        var emailTaken = await _context.Clients
            .AnyAsync(c =>
                c.Id != clientId &&
                c.Email != null &&
                c.Email.ToLower() == normalizedEmail &&
                !c.IsDeleted);

        if (emailTaken)
            throw new InvalidOperationException("Another client already has this email.");

        client.Email = normalizedEmail;
        client.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        var events = await _context.ExternalCalendarEvents
            .Where(e =>
                e.CalendarIntegration.UserId == _currentUser.UserId &&
                e.AttendeesJson != null &&
                e.AttendeesJson.ToLower().Contains(normalizedEmail))
            .ToListAsync();

        foreach (var evt in events)
        {
            if (evt.SessionId == null)
                continue;

            var session = await _context.Sessions
                .Include(s => s.Participants)
                .FirstOrDefaultAsync(s => s.Id == evt.SessionId);

            if (session == null || session.Status == "Completed" || session.Status == "Cancelled")
                continue;
            if (!_currentUser.IsOwner && !await _context.Trainers.AnyAsync(t => t.Id == session.TrainerId && t.UserId == _currentUser.UserId))
                continue;
            if (session.PublicCapacity.HasValue && session.Participants.Count(p => p.AttendanceStatus != "CancelledInTime" && p.AttendanceStatus != "CancelledLate") >= session.PublicCapacity.Value)
                continue;

            var alreadyExists = session.Participants.Any(p => p.ClientId == clientId);

            if (!alreadyExists)
            {
                var activeClientPackage = await _context.ClientPackages
                    .Where(cp => cp.ClientId == client.Id && cp.IsActive)
                    .OrderByDescending(cp => cp.PurchaseDate)
                    .FirstOrDefaultAsync();

                await _context.SessionParticipants.AddAsync(new SessionParticipant
                {
                    SessionId = session.Id,
                    ClientId = clientId,
                    PackageId = activeClientPackage?.PackageId,
                    ClientPackageId = activeClientPackage?.Id,
                    AttendanceStatus = "Planned",
                    CountsAgainstPackage = true,
                    SessionsCharged = 1,
                    PlannedBillingType = activeClientPackage?.ExpectedBillingType,
                    ExpectedUnitPrice = activeClientPackage?.ExpectedUnitPrice,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                });
            }

            var sessionClients = await _context.SessionParticipants
                .Where(p => p.SessionId == session.Id)
                .Include(p => p.Client)
                .Select(p => p.Client)
                .ToListAsync();

            if (!sessionClients.Any(c => c.Id == client.Id))
                sessionClients.Add(client);

            if (SessionTitleBuilder.ShouldDeriveFromParticipants(
                session.IsPubliclyBookable,
                session.PlannedSessionType))
            {
                session.Title = SessionTitleBuilder.Build(sessionClients);
            }
            session.UpdatedAt = DateTime.UtcNow;

            var warnings = ReadWarnings(evt.MappingWarningsJson);
            warnings = warnings
                .Where(w => !w.Contains(normalizedEmail, StringComparison.OrdinalIgnoreCase))
                .ToList();

            evt.MappingWarningsJson = JsonSerializer.Serialize(warnings);
        }

        await _context.SaveChangesAsync();
    }

    public async Task IgnoreIssueAsync(int externalEventId, string message)
    {
        var evt = await _context.ExternalCalendarEvents
            .FirstOrDefaultAsync(x => x.Id == externalEventId && (_currentUser.IsOwner || x.CalendarIntegration.UserId == _currentUser.UserId));

        if (evt == null)
            throw new InvalidOperationException("Event not found.");

        var warnings = ReadWarnings(evt.MappingWarningsJson);

        warnings = warnings
            .Where(w => w != message)
            .ToList();

        evt.MappingWarningsJson = JsonSerializer.Serialize(warnings);

        await _context.SaveChangesAsync();
    }

    private static List<string> ReadWarnings(string? warningsJson)
    {
        if (string.IsNullOrWhiteSpace(warningsJson))
            return new List<string>();

        try
        {
            return JsonSerializer.Deserialize<List<string>>(warningsJson) ?? new List<string>();
        }
        catch
        {
            return new List<string>();
        }
    }

    private static List<string> ReadStringList(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return new List<string>();

        try
        {
            return JsonSerializer.Deserialize<List<string>>(json) ?? new List<string>();
        }
        catch
        {
            return new List<string>();
        }
    }

    private static List<OutlookCategoryDto> ReadOutlookCategoryColors(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return new List<OutlookCategoryDto>();

        try
        {
            return JsonSerializer.Deserialize<List<OutlookCategoryDto>>(json)
                ?? new List<OutlookCategoryDto>();
        }
        catch
        {
            return new List<OutlookCategoryDto>();
        }
    }

    private static string ResolveIssueType(string warning)
    {
        var review = OutlookReviewIssues.ResolveType(warning);
        if (review != null)
            return review;

        var normalized = warning.ToLowerInvariant();

        if (normalized.Contains("nie znaleziono klienta"))
            return "UnknownClient";

        if (normalized.Contains("nie rozpoznano lokalizacji"))
            return "UnknownLocation";

        if (normalized.Contains("limit sali"))
            return "LocationLimitExceeded";

        if (normalized.Contains("trenera"))
            return "UnknownTrainer";

        return "Other";
    }

    private static string? ExtractEmailFromWarning(string warning)
    {
        // Review issues are sentences; only an address inside them (removed attendee) is an email.
        if (OutlookReviewIssues.IsReview(warning))
        {
            var match = System.Text.RegularExpressions.Regex.Match(warning, @"[^\s:]+@[^\s:]+\.[A-Za-z]{2,}");
            return match.Success ? match.Value : null;
        }

        var parts = warning.Split(':', StringSplitOptions.TrimEntries);

        if (parts.Length < 2)
            return null;

        var candidate = parts.LastOrDefault();

        return string.IsNullOrWhiteSpace(candidate)
            ? null
            : candidate;
    }

}
