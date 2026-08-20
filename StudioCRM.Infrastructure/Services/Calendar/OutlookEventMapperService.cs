using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using StudioCRM.Domain.Entities;
using StudioCRM.Domain.Enums;
using StudioCRM.Infrastructure.Persistence;

namespace StudioCRM.Infrastructure.Services.Calendar;

public class OutlookEventMapperService
{
    private const int LocationPeopleLimit = 8;

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

        var matchingSession = await FindMatchingSessionAsync(evt, trainer, location, clients);

        if (matchingSession is not null)
        {
            await AddMissingClientsToSessionAsync(matchingSession, clients, warnings);
            matchingSession.Title = await BuildSessionTitleFromParticipantsAsync(matchingSession.Id);
            matchingSession.UpdatedAt = DateTime.UtcNow;

            var sessionLink = await _context.CalendarEventLinks
                .FirstOrDefaultAsync(x =>
                    x.SessionId == matchingSession.Id &&
                    x.Provider == evt.Provider);

            if (sessionLink is null)
            {
                await _context.CalendarEventLinks.AddAsync(new CalendarEventLink
                {
                    SessionId = matchingSession.Id,
                    CalendarIntegrationId = evt.CalendarIntegrationId,
                    Provider = evt.Provider,
                    ExternalEventId = evt.ExternalEventId,
                    SyncedAt = DateTime.UtcNow
                });
            }

            evt.IsConvertedToSession = true;
            evt.SessionId = matchingSession.Id;
            matchingSession.OutlookCategoriesJson = evt.CategoriesJson;
            matchingSession.OutlookCategoryColorsJson = evt.CategoryColorsJson;
            matchingSession.PrimaryOutlookCategory = GetPrimaryCategory(evt.CategoriesJson);

            warnings.Add("Event dopięto do istniejącej sesji CRM zamiast tworzyć drugą sesję w tym samym czasie.");
            await SaveWarningsAsync(evt, warnings);

            return (matchingSession, warnings);
        }

        var overlappingSession = await FindOverlappingSessionAsync(evt, trainer, location);

        if (overlappingSession is not null)
        {
            warnings.Add(
                $"Event nakłada się z istniejącą sesją CRM #{overlappingSession.Id}, ale nie ma dokładnie tego samego czasu. Pominięto automatyczne tworzenie i dopisywanie klientów.");
            await SaveWarningsAsync(evt, warnings);

            return (null, warnings);
        }

        var session = new Session
        {
            Title = BuildSessionTitle(clients),
            Note = evt.BodyPreview,
            StartAt = evt.StartAt,
            EndAt = evt.EndAt,
            TrainerId = trainer.Id,
            LocationId = location.Id,
            Status = "Planned",

            OutlookCategoriesJson = evt.CategoriesJson,
            OutlookCategoryColorsJson = evt.CategoryColorsJson,
            PrimaryOutlookCategory = GetPrimaryCategory(evt.CategoriesJson),

            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            CreatedBy = trainer.UserId
        };

        await _context.Sessions.AddAsync(session);
        await _context.SaveChangesAsync();

        foreach (var client in clients.DistinctBy(c => c.Id))
        {
            await AddClientToSessionAsync(session.Id, client);
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

        var locationPeopleCount = await CountPeopleInLocationForTimeRangeAsync(
            location.Id,
            evt.StartAt,
            evt.EndAt);

        if (locationPeopleCount > LocationPeopleLimit)
        {
            warnings.Add($"Limit lokalizacji przekroczony: {locationPeopleCount}/{LocationPeopleLimit}");
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

    private async Task<int> CountPeopleInLocationForTimeRangeAsync(
        int locationId,
        DateTime startAt,
        DateTime endAt)
    {
        var overlappingSessions = await _context.Sessions
            .Where(s =>
                !s.IsDeleted &&
                s.Status != "Cancelled" &&
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

    private async Task<Session?> FindMatchingSessionAsync(
        ExternalCalendarEvent evt,
        Trainer trainer,
        Location location,
        List<Client> clients)
    {
        var clientIds = clients
            .Select(c => c.Id)
            .Distinct()
            .OrderBy(id => id)
            .ToList();

        if (clientIds.Count == 0)
            return null;

        var candidates = await _context.Sessions
            .Include(s => s.Participants)
            .Where(s =>
                !s.IsDeleted &&
                s.TrainerId == trainer.Id &&
                s.LocationId == location.Id &&
                s.Status != "Cancelled" &&
                s.StartAt == evt.StartAt &&
                s.EndAt == evt.EndAt)
            .OrderBy(s => s.StartAt)
            .ToListAsync();

        return candidates.FirstOrDefault();
    }

    private async Task<Session?> FindOverlappingSessionAsync(
        ExternalCalendarEvent evt,
        Trainer trainer,
        Location location)
    {
        return await _context.Sessions
            .Where(s =>
                !s.IsDeleted &&
                s.TrainerId == trainer.Id &&
                s.LocationId == location.Id &&
                s.Status != "Cancelled" &&
                s.StartAt < evt.EndAt &&
                s.EndAt > evt.StartAt)
            .OrderBy(s => s.StartAt)
            .FirstOrDefaultAsync();
    }

    private async Task AddMissingClientsToSessionAsync(
        Session session,
        List<Client> clients,
        List<string> warnings)
    {
        var existingClientIds = session.Participants
            .Select(p => p.ClientId)
            .ToHashSet();

        var missingClients = clients
            .DistinctBy(c => c.Id)
            .Where(c => !existingClientIds.Contains(c.Id))
            .ToList();

        if (missingClients.Count == 0)
            return;

        if (session.Participants.Count + missingClients.Count > 4)
        {
            warnings.Add(
                $"Nie dopisano klientów do sesji CRM #{session.Id}, bo trener może prowadzić maksymalnie 4 osoby w jednej sesji.");
            return;
        }

        foreach (var client in missingClients)
        {
            await AddClientToSessionAsync(session.Id, client);
        }

        await _context.SaveChangesAsync();

        warnings.Add($"Dopisano {missingClients.Count} klient(ów) do istniejącej sesji CRM #{session.Id}.");
    }

    private async Task AddClientToSessionAsync(int sessionId, Client client)
    {
        var activeClientPackage = await _context.ClientPackages
            .Where(cp => cp.ClientId == client.Id && cp.IsActive)
            .OrderByDescending(cp => cp.PurchaseDate)
            .FirstOrDefaultAsync();

        await _context.SessionParticipants.AddAsync(new SessionParticipant
        {
            SessionId = sessionId,
            ClientId = client.Id,
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

    private async Task<string> BuildSessionTitleFromParticipantsAsync(int sessionId)
    {
        var clients = await _context.SessionParticipants
            .Where(p => p.SessionId == sessionId)
            .Include(p => p.Client)
            .Select(p => p.Client)
            .ToListAsync();

        return BuildSessionTitle(clients);
    }

    private async Task SaveWarningsAsync(ExternalCalendarEvent evt, List<string> warnings)
    {
        evt.MappingWarningsJson = JsonSerializer.Serialize(warnings.Distinct().ToList());
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

    private static string? GetPrimaryCategory(string? categoriesJson)
    {
        if (string.IsNullOrWhiteSpace(categoriesJson))
            return null;

        try
        {
            var categories = JsonSerializer.Deserialize<List<string>>(categoriesJson);
            return categories?.FirstOrDefault();
        }
        catch
        {
            return null;
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

        return string.Join(" + ", clients
            .DistinctBy(c => c.Id)
            .OrderBy(c => c.FirstName)
            .ThenBy(c => c.LastName)
            .Select(c =>
            {
                var firstName = c.FirstName;

                var lastInitial = string.IsNullOrWhiteSpace(c.LastName)
                    ? string.Empty
                    : $"{c.LastName[0]}";

                return $"{firstName} {lastInitial}".Trim();
            }));
    }

}
