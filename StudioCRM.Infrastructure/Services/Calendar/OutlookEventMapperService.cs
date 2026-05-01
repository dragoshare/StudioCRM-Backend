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

        var calendarIntegration = await _context.CalendarIntegrations
            .FirstOrDefaultAsync(x => x.Id == evt.CalendarIntegrationId);

        if (calendarIntegration == null)
        {
            warnings.Add("Brak integracji kalendarza");
            return (null, warnings);
        }

        // 🔹 TRAINER
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
            return (null, warnings);
        }

        // 🔹 LOCATION
        var location = await _context.Locations
            .FirstOrDefaultAsync(l =>
                l.CalendarEmail != null &&
                l.CalendarEmail.ToLower() == locationEmail);

        if (location == null)
        {
            warnings.Add($"Nie rozpoznano lokalizacji: {evt.LocationEmail}");
            return (null, warnings);
        }

        // 🔹 ATTENDEES
        var attendeeEmails = ReadAttendeeEmails(evt.AttendeesJson)
            .Select(NormalizeEmail)
            .Where(e =>
                !string.IsNullOrWhiteSpace(e) &&
                e != organizerEmail &&
                e != locationEmail)
            .Distinct()
            .ToList();

        var clients = new List<Client>();

        foreach (var email in attendeeEmails)
        {
            var client = await _context.Clients
                .FirstOrDefaultAsync(c => c.Email.ToLower() == email);

            if (client == null)
            {
                warnings.Add($"Nie znaleziono klienta: {email}");
                continue;
            }

            clients.Add(client);
        }

        // 🔹 DUPLIKAT
        var exists = await _context.CalendarEventLinks
            .AnyAsync(x =>
                x.ExternalEventId == evt.ExternalEventId &&
                x.Provider == evt.Provider);

        if (exists || evt.IsConvertedToSession)
        {
            warnings.Add("Event już zmapowany");
            return (evt.Session, warnings);
        }

        // 🔹 SESSION
        var session = new Session
        {
            Title = string.IsNullOrWhiteSpace(evt.Subject)
                ? BuildTitle(clients)
                : evt.Subject,

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

        _context.Sessions.Add(session);
        await _context.SaveChangesAsync();

        // 🔹 PARTICIPANTS
        foreach (var client in clients)
        {
            _context.SessionParticipants.Add(new SessionParticipant
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

        // 🔹 LINK
        _context.CalendarEventLinks.Add(new CalendarEventLink
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

        // 🔹 LIMIT SALI
        var peopleCount = await CountPeople(location.Id, evt.StartAt, evt.EndAt);

        if (peopleCount > RoomPeopleLimit)
        {
            warnings.Add($"Limit sali przekroczony: {peopleCount}/8");
        }

        evt.MappingWarningsJson = JsonSerializer.Serialize(warnings);
        await _context.SaveChangesAsync();

        return (session, warnings);
    }

    private async Task<int> CountPeople(int locationId, DateTime start, DateTime end)
    {
        var sessions = await _context.Sessions
            .Where(s =>
                s.LocationId == locationId &&
                s.StartAt < end &&
                s.EndAt > start)
            .ToListAsync();

        var sessionIds = sessions.Select(s => s.Id).ToList();

        var clients = await _context.SessionParticipants
            .CountAsync(p => sessionIds.Contains(p.SessionId));

        var trainers = sessions
            .Select(s => s.TrainerId)
            .Distinct()
            .Count();

        return clients + trainers;
    }

    private static List<string> ReadAttendeeEmails(string? json)
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

    private static string NormalizeEmail(string? email)
    {
        return (email ?? "").Trim().ToLower();
    }

    private static string BuildTitle(List<Client> clients)
    {
        if (clients.Count == 0)
            return "Trening";

        return string.Join(", ", clients.Select(c =>
            $"{c.FirstName} {(string.IsNullOrEmpty(c.LastName) ? "" : c.LastName[0] + ".")}"
        ));
    }
}