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
            Warnings = ReadWarnings(x.MappingWarningsJson)
        }).ToList();
    }

    public async Task<List<OutlookMappingIssueDto>> GetIssuesAsync()
    {
        if (!_currentUser.UserId.HasValue)
            return new List<OutlookMappingIssueDto>();

        var events = await _context.ExternalCalendarEvents
            .Include(x => x.CalendarIntegration)
            .Where(x =>
                x.CalendarIntegration.UserId == _currentUser.UserId.Value &&
                x.MappingWarningsJson != null)
            .OrderByDescending(x => x.StartAt)
            .ToListAsync();

        var result = new List<OutlookMappingIssueDto>();

        foreach (var calendarEvent in events)
        {
            var warnings = ReadWarnings(calendarEvent.MappingWarningsJson);

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

        var importedEvent = await _context.ExternalCalendarEvents
            .Include(x => x.CalendarIntegration)
            .Include(x => x.Session)
            .FirstOrDefaultAsync(x =>
                x.Id == importedEventId &&
                x.CalendarIntegration.UserId == _currentUser.UserId.Value);

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
            .FirstOrDefaultAsync(c => c.Id == clientId && !c.IsDeleted);

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

            if (session == null)
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

            session.Title = SessionTitleBuilder.Build(sessionClients);
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
            .FirstOrDefaultAsync(x => x.Id == externalEventId);

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

    private static string ResolveIssueType(string warning)
    {
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
        var parts = warning.Split(':', StringSplitOptions.TrimEntries);

        if (parts.Length < 2)
            return null;

        var candidate = parts.LastOrDefault();

        return string.IsNullOrWhiteSpace(candidate)
            ? null
            : candidate;
    }

}
