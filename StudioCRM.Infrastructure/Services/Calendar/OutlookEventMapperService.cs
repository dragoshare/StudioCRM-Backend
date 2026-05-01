using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using StudioCRM.Domain.Entities;
using StudioCRM.Infrastructure.Persistence;

namespace StudioCRM.Infrastructure.Services.Calendar;

public class OutlookEventMapperService
{
    private const int RoomPeopleLimit = 8;

    private readonly StudioCRMDbContext _context;

    public OutlookEventMapperService(StudioCRMDbContext context)
    {
        _context = context;
    }

    public async Task<(Session? Session, List<string> Warnings)> MapToSessionAsync(ExternalCalendarEvent evt)
    {
        var warnings = new List<string>();

        var organizerEmail = NormalizeEmail(evt.OrganizerEmail);
        var locationEmail = NormalizeEmail(evt.LocationEmail);
        var locationName = NormalizeText(evt.LocationName);

        var calendarIntegration = await _context.CalendarIntegrations
            .FirstOrDefaultAsync(x => x.Id == evt.CalendarIntegrationId);

        if (calendarIntegration == null)
        {
            warnings.Add("Brak integracji kalendarza.");
            await SaveWarningsAsync(evt, warnings);
            return (null, warnings);
        }

        var trainer = await _context.Trainers
            .Include(t => t.User)
            .FirstOrDefaultAsync(t => t.User.Email.ToLower() == organizerEmail);

        if (trainer == null)
        {
            trainer = await _context.Trainers
                .Include(t => t.User)
                .FirstOrDefaultAsync(t => t.UserId == calendarIntegration.UserId);
        }

        if (trainer == null)
        {
            warnings.Add($"Nie rozpoznano trenera: {evt.OrganizerEmail}");
            await SaveWarningsAsync(evt, warnings);
            return (null, warnings);
        }

        var location = await FindLocationAsync(locationEmail, locationName);

        if (location == null)
        {
            warnings.Add($"Nie rozpoznano lokalizacji: {evt.LocationEmail ?? evt.LocationName}");
            await SaveWarningsAsync(evt, warnings);
            return (null, warnings);
        }

        var attendeeEmails = ReadAttendeeEmails(evt.AttendeesJson)
            .Select(NormalizeEmail)
            .Where(e =>
                !string.IsNullOrWhiteSpace(e) &&
                e != organizerEmail &&
                e != locationEmail &&
                e != NormalizeEmail(trainer.User.Email))
            .Distinct()
            .ToList();

        var clients = new List<Client>();

        foreach (var email in attendeeEmails)
        {
            var client = await _context.Clients
                .FirstOrDefaultAsync(c =>
                    c.Email.ToLower() == email &&
                    !c.IsDeleted);

            if (client == null)
            {
                warnings.Add($"Nie znaleziono klienta: {email}");
                continue;
            }

            clients.Add(client);
        }

        var existingLink = await _context.CalendarEventLinks
            .Include(x => x.Session)
            .FirstOrDefaultAsync(x =>
                x.Provider == evt.Provider &&
                x.ExternalEventId == evt.ExternalEventId);

        if (existingLink != null || evt.IsConvertedToSession)
        {
            warnings.Add("Event jest już połączony z sesją CRM.");
            await SaveWarningsAsync(evt, warnings);

            return (existingLink?.Session ?? evt.Session, warnings);
        }

        var session = new Session
        {
            Title = BuildSessionTitle(clients),
            Note = evt.BodyPreview,
            StartAt = evt.StartAt,
            EndAt = evt.EndAt,
            TrainerId = trainer.Id,
            LocationId = location.Id,
            StudioRoom = location.Name,
            Status = "Planned",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            CreatedBy = trainer.UserId
        };

        await _context.Sessions.AddAsync(session);
        await _context.SaveChangesAsync();

        foreach (var client in clients)
        {
            var alreadyAdded = await _context.SessionParticipants
                .AnyAsync(p => p.SessionId == session.Id && p.ClientId == client.Id);

            if (alreadyAdded)
                continue;

            await _context.SessionParticipants.AddAsync(new SessionParticipant
            {
                SessionId = session.Id,
                ClientId = client.Id,
                PackageId = client.ActivePackageId,
                AttendanceStatus = "Planned",
                CountsAgainstPackage = true,
                SessionsCharged = 1,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });
        }

        await _context.CalendarEventLinks.AddAsync(new CalendarEventLink
        {
            SessionId = session.Id,
            CalendarIntegrationId = evt.CalendarIntegrationId,
            Provider = evt.Provider,
            ExternalEventId = evt.ExternalEventId,
            SyncedAt = DateTime.UtcNow
        });

        evt.IsConvertedToSession = true;
        evt.SessionId = session.Id;

        await _context.SaveChangesAsync();

        var roomPeopleCount = await CountPeopleInRoomForTimeRangeAsync(
            location.Id,
            evt.StartAt,
            evt.EndAt);

        if (roomPeopleCount > RoomPeopleLimit)
        {
            warnings.Add($"Limit sali przekroczony: {roomPeopleCount}/{RoomPeopleLimit}");
        }

        await SaveWarningsAsync(evt, warnings);

        return (session, warnings);
    }

    private async Task<Location?> FindLocationAsync(string locationEmail, string locationName)
    {
        if (!string.IsNullOrWhiteSpace(locationEmail))
        {
            var byEmail = await _context.Locations
                .FirstOrDefaultAsync(l =>
                    l.IsActive &&
                    l.CalendarEmail != null &&
                    l.CalendarEmail.ToLower() == locationEmail);

            if (byEmail != null)
                return byEmail;
        }

        if (string.IsNullOrWhiteSpace(locationName))
            return null;

        var locations = await _context.Locations
            .Where(l => l.IsActive)
            .ToListAsync();

        return locations.FirstOrDefault(l =>
        {
            var crmLocationName = NormalizeText(l.Name);
            var crmCalendarEmail = NormalizeText(l.CalendarEmail);

            return
                locationName == crmLocationName ||
                locationName.Contains(crmLocationName) ||
                crmLocationName.Contains(locationName) ||
                (!string.IsNullOrWhiteSpace(crmCalendarEmail) &&
                 locationName.Contains(crmCalendarEmail));
        });
    }

    private async Task<int> CountPeopleInRoomForTimeRangeAsync(
        int locationId,
        DateTime startAt,
        DateTime endAt)
    {
        var overlappingSessions = await _context.Sessions
            .Where(s =>
                !s.IsDeleted &&
                s.LocationId == locationId &&
                s.StartAt < endAt &&
                s.EndAt > startAt)
            .Select(s => new
            {
                s.Id,
                s.TrainerId
            })
            .ToListAsync();

        var overlappingSessionIds = overlappingSessions
            .Select(s => s.Id)
            .ToList();

        var clientsCount = await _context.SessionParticipants
            .Where(p => overlappingSessionIds.Contains(p.SessionId))
            .CountAsync();

        var trainersCount = overlappingSessions
            .Select(s => s.TrainerId)
            .Distinct()
            .Count();

        return clientsCount + trainersCount;
    }

    private async Task SaveWarningsAsync(ExternalCalendarEvent evt, List<string> warnings)
    {
        evt.MappingWarningsJson = JsonSerializer.Serialize(warnings);
        await _context.SaveChangesAsync();
    }

    private static List<string> ReadAttendeeEmails(string? attendeesJson)
    {
        if (string.IsNullOrWhiteSpace(attendeesJson))
            return new List<string>();

        try
        {
            return JsonSerializer.Deserialize<List<string>>(attendeesJson) ?? new List<string>();
        }
        catch
        {
            return new List<string>();
        }
    }

    private static string NormalizeEmail(string? email)
    {
        return (email ?? string.Empty).Trim().ToLowerInvariant();
    }

    private static string NormalizeText(string? value)
    {
        return (value ?? string.Empty).Trim().ToLowerInvariant();
    }

    private static string BuildSessionTitle(List<Client> clients)
    {
        if (clients.Count == 0)
            return "Trening";

        return string.Join(" + ", clients.Select(c =>
        {
            var firstName = c.FirstName;

            var lastInitial = string.IsNullOrWhiteSpace(c.LastName)
                ? string.Empty
                : $"{c.LastName[0]}";

            return $"{firstName} {lastInitial}".Trim();
        }));
    }
}
