using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StudioCRM.Application.Common;
using StudioCRM.Application.DTOs.Sessions;
using StudioCRM.Application.Interfaces.Calendar;
using StudioCRM.Application.Settings;
using StudioCRM.Domain.Entities;
using StudioCRM.Infrastructure.Persistence;

namespace StudioCRM.Infrastructure.Services.Calendar;

public class OutlookCalendarSyncService : IOutlookCalendarSyncService
{
    private const string OutlookStudioTimeZone = "Central European Standard Time";

    private readonly StudioCRMDbContext _context;
    private readonly OutlookSettings _settings;
    private readonly HttpClient _httpClient;
    private readonly ILogger<OutlookCalendarSyncService> _logger;

    public OutlookCalendarSyncService(
        StudioCRMDbContext context,
        IOptions<OutlookSettings> options,
        HttpClient httpClient,
        ILogger<OutlookCalendarSyncService> logger)
    {
        _context = context;
        _settings = options.Value;
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task SyncSessionAsync(int sessionId)
    {
        var session = await _context.Sessions
            .Include(s => s.Trainer)
                .ThenInclude(t => t.User)
            .Include(s => s.Participants)
            .ThenInclude(p => p.Client)
            .Include(s => s.Location)
            .FirstOrDefaultAsync(s => s.Id == sessionId);

        if (session is null)
            throw new InvalidOperationException("Session does not exist.");

        var integration = await GetTrainerIntegrationAsync(session.Trainer.UserId);

        if (integration is null)
            throw new InvalidOperationException("Trainer does not have active Outlook integration.");

        await EnsureAccessTokenAsync(integration);

        var existingLink = await _context.CalendarEventLinks
            .FirstOrDefaultAsync(x =>
                x.SessionId == session.Id &&
                x.Provider == "Outlook");

        if (existingLink is null)
        {
            var existingExternalEvent = await _context.ExternalCalendarEvents
                .FirstOrDefaultAsync(x =>
                    x.SessionId == session.Id &&
                    x.Provider == "Outlook" &&
                    x.CalendarIntegrationId == integration.Id &&
                    x.ExternalEventId != string.Empty &&
                    !x.Subject.StartsWith("[DELETED]"));

            if (existingExternalEvent is not null)
            {
                existingLink = new CalendarEventLink
                {
                    SessionId = session.Id,
                    CalendarIntegrationId = integration.Id,
                    Provider = "Outlook",
                    ExternalEventId = existingExternalEvent.ExternalEventId,
                    SyncedAt = DateTime.UtcNow
                };

                await _context.CalendarEventLinks.AddAsync(existingLink);
            }
        }

        if (existingLink is null)
        {
            var externalEventId = await CreateEventAsync(session, integration.AccessToken);

            var link = new CalendarEventLink
            {
                SessionId = session.Id,
                CalendarIntegrationId = integration.Id,
                Provider = "Outlook",
                ExternalEventId = externalEventId,
                SyncedAt = DateTime.UtcNow
            };

            await _context.CalendarEventLinks.AddAsync(link);

            await UpsertExternalCalendarEventAsync(session, integration.Id, externalEventId);
        }
        else
        {
            if (existingLink.CalendarIntegrationId != integration.Id)
            {
                await DeleteLinkedEventAsync(existingLink);

                var externalEventId = await CreateEventAsync(session, integration.AccessToken);

                existingLink.CalendarIntegrationId = integration.Id;
                existingLink.ExternalEventId = externalEventId;
            }
            else
            {
                var existingEventUpdated = await UpdateEventAsync(
                    session,
                    integration.AccessToken,
                    existingLink.ExternalEventId);

                if (!existingEventUpdated)
                    existingLink.ExternalEventId = await CreateEventAsync(session, integration.AccessToken);
            }

            existingLink.SyncedAt = DateTime.UtcNow;

            await UpsertExternalCalendarEventAsync(session, integration.Id, existingLink.ExternalEventId);
        }

        await _context.SaveChangesAsync();
    }

    public async Task SyncSessionSeriesAsync(
        string recurringGroupId,
        SessionRecurrenceDto recurrence)
    {
        var sessions = await _context.Sessions
            .Include(s => s.Trainer)
                .ThenInclude(t => t.User)
            .Include(s => s.Participants)
                .ThenInclude(p => p.Client)
            .Include(s => s.Location)
            .Where(s => s.RecurringGroupId == recurringGroupId && s.IsRecurring)
            .OrderBy(s => s.RecurrenceInstanceNumber)
            .ToListAsync();

        if (sessions.Count < 2)
            throw new InvalidOperationException("Session series does not exist or has fewer than two occurrences.");

        if (sessions.Select(s => s.TrainerId).Distinct().Count() != 1)
            throw new InvalidOperationException("All sessions in an Outlook series must have the same trainer.");

        var integration = await GetTrainerIntegrationAsync(sessions[0].Trainer.UserId)
            ?? throw new InvalidOperationException("Trainer does not have active Outlook integration.");
        await EnsureAccessTokenAsync(integration);

        var sessionIds = sessions.Select(s => s.Id).ToList();
        var alreadyLinkedCount = await _context.CalendarEventLinks.CountAsync(x =>
            x.Provider == "Outlook" && sessionIds.Contains(x.SessionId));
        if (alreadyLinkedCount == sessions.Count)
            return;
        if (alreadyLinkedCount > 0)
            throw new InvalidOperationException("Session series is only partially linked to Outlook and requires reconciliation.");

        var masterId = await CreateSeriesMasterEventAsync(sessions, recurrence, integration.AccessToken);
        try
        {
            var instances = await GetSeriesInstancesWithRetryAsync(
                masterId,
                integration.AccessToken,
                sessions[0].StartAt.AddDays(-1),
                sessions[^1].EndAt.AddDays(1),
                sessions.Count);

            for (var index = 0; index < sessions.Count; index++)
            {
                var session = sessions[index];
                var instance = instances[index];
                if (Math.Abs((instance.StartAt - session.StartAt).TotalMinutes) > 1)
                    throw new InvalidOperationException("Outlook occurrence dates do not match the CRM series.");

                await _context.CalendarEventLinks.AddAsync(new CalendarEventLink
                {
                    SessionId = session.Id,
                    CalendarIntegrationId = integration.Id,
                    Provider = "Outlook",
                    ExternalEventId = instance.Id,
                    SyncedAt = DateTime.UtcNow
                });
                await UpsertExternalCalendarEventAsync(
                    session,
                    integration.Id,
                    instance.Id,
                    masterId,
                    isRecurring: true);
            }

            await _context.SaveChangesAsync();
        }
        catch
        {
            await TryDeleteEventAsync(masterId, integration.AccessToken);
            throw;
        }
    }

    public async Task DeleteSessionEventAsync(int sessionId)
    {
        var link = await _context.CalendarEventLinks
            .Include(x => x.CalendarIntegration)
            .FirstOrDefaultAsync(x =>
                x.SessionId == sessionId &&
                x.Provider == "Outlook");

        if (link is null)
            return;

        var integration = link.CalendarIntegration;

        if (!integration.IsActive)
            return;

        await DeleteLinkedEventAsync(link);
        _context.CalendarEventLinks.Remove(link);
        await _context.SaveChangesAsync();
    }

    public async Task<bool> DeleteSessionSeriesAsync(string recurringGroupId)
    {
        var sessionIds = await _context.Sessions
            .Where(session => session.RecurringGroupId == recurringGroupId && session.IsRecurring)
            .Select(session => session.Id)
            .ToListAsync();
        if (sessionIds.Count == 0)
            return false;

        var links = await _context.CalendarEventLinks
            .Include(link => link.CalendarIntegration)
            .Where(link => link.Provider == "Outlook" && sessionIds.Contains(link.SessionId))
            .ToListAsync();
        var externalEvents = await _context.ExternalCalendarEvents
            .Where(calendarEvent =>
                calendarEvent.Provider == "Outlook" &&
                calendarEvent.SessionId.HasValue &&
                sessionIds.Contains(calendarEvent.SessionId.Value))
            .ToListAsync();
        var masterTargets = externalEvents
            .Where(calendarEvent => !string.IsNullOrWhiteSpace(calendarEvent.SeriesMasterId))
            .GroupBy(calendarEvent => new
            {
                calendarEvent.CalendarIntegrationId,
                MasterId = calendarEvent.SeriesMasterId!
            })
            .Select(group => group.Key)
            .ToList();
        var deletedRemoteEvent = false;

        foreach (var target in masterTargets)
        {
            var integration = links
                .Select(link => link.CalendarIntegration)
                .FirstOrDefault(item => item.Id == target.CalendarIntegrationId)
                ?? await _context.CalendarIntegrations.FirstOrDefaultAsync(item =>
                    item.Id == target.CalendarIntegrationId);
            if (integration is null || !integration.IsActive)
                throw new InvalidOperationException("Outlook integration for this series is not active.");

            await DeleteEventAsync(integration, target.MasterId);
            deletedRemoteEvent = true;
        }

        if (masterTargets.Count == 0)
        {
            foreach (var link in links.DistinctBy(item => item.ExternalEventId))
            {
                if (!link.CalendarIntegration.IsActive)
                    throw new InvalidOperationException("Outlook integration for this series is not active.");

                await DeleteEventAsync(link.CalendarIntegration, link.ExternalEventId);
                deletedRemoteEvent = true;
            }
        }

        _context.CalendarEventLinks.RemoveRange(links);
        foreach (var calendarEvent in externalEvents)
        {
            if (!calendarEvent.Subject.StartsWith("[DELETED]", StringComparison.OrdinalIgnoreCase))
                calendarEvent.Subject = "[DELETED] " + calendarEvent.Subject;
            calendarEvent.SessionId = null;
            calendarEvent.IsConvertedToSession = false;
        }

        await _context.SaveChangesAsync();
        return deletedRemoteEvent;
    }

    private async Task<string> CreateEventAsync(Session session, string accessToken)
    {
        var categories = ResolveGraphEventCategories(session);
        await EnsureTrainerMasterCategoryAsync(session, accessToken, categories);
        var payload = BuildGraphEventPayload(session, categories);

        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            "https://graph.microsoft.com/v1.0/me/events");

        request.Headers.Authorization =
            new AuthenticationHeaderValue("Bearer", accessToken);

        request.Content = new StringContent(
            JsonSerializer.Serialize(payload),
            Encoding.UTF8,
            "application/json");

        var response = await _httpClient.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException($"Microsoft create event error: {body}");

        using var doc = JsonDocument.Parse(body);

        return doc.RootElement.GetProperty("id").GetString()
            ?? throw new InvalidOperationException("Graph event id is missing.");
    }

    private async Task<bool> UpdateEventAsync(Session session, string accessToken, string eventId)
    {
        var categories = ResolveGraphEventCategories(session);
        await EnsureTrainerMasterCategoryAsync(session, accessToken, categories);
        var payload = BuildGraphEventPayload(session, categories);

        using var request = new HttpRequestMessage(
            HttpMethod.Patch,
            $"https://graph.microsoft.com/v1.0/me/events/{eventId}");

        request.Headers.Authorization =
            new AuthenticationHeaderValue("Bearer", accessToken);

        request.Content = new StringContent(
            JsonSerializer.Serialize(payload),
            Encoding.UTF8,
            "application/json");

        var response = await _httpClient.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();

        if (response.StatusCode is System.Net.HttpStatusCode.NotFound or System.Net.HttpStatusCode.Gone)
            return false;

        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException($"Microsoft update event error: {body}");

        return true;
    }

    private async Task<string> CreateSeriesMasterEventAsync(
        List<Session> sessions,
        SessionRecurrenceDto recurrence,
        string accessToken)
    {
        var first = sessions[0];
        var categories = ResolveGraphEventCategories(first);
        await EnsureTrainerMasterCategoryAsync(first, accessToken, categories);
        var payload = BuildGraphEventPayload(first, categories);
        var frequency = recurrence.Frequency.Trim().Equals("Daily", StringComparison.OrdinalIgnoreCase)
            ? "daily"
            : "weekly";
        var pattern = new Dictionary<string, object?>
        {
            ["type"] = frequency,
            ["interval"] = recurrence.Interval
        };
        if (frequency == "weekly")
        {
            pattern["daysOfWeek"] = (recurrence.DaysOfWeek?.Count > 0
                    ? recurrence.DaysOfWeek
                    : new List<string> { ToStudioLocalTime(first.StartAt).DayOfWeek.ToString() })
                .Select(day => day.Trim().ToLowerInvariant())
                .Distinct()
                .ToList();
            pattern["firstDayOfWeek"] = "monday";
        }

        payload["recurrence"] = new
        {
            pattern,
            range = new
            {
                type = "numbered",
                startDate = ToStudioLocalTime(first.StartAt).ToString("yyyy-MM-dd"),
                numberOfOccurrences = sessions.Count,
                recurrenceTimeZone = OutlookStudioTimeZone
            }
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, "https://graph.microsoft.com/v1.0/me/events");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        request.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
        var response = await _httpClient.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException($"Microsoft create recurring event error: {body}");

        using var doc = JsonDocument.Parse(body);
        return doc.RootElement.GetProperty("id").GetString()
            ?? throw new InvalidOperationException("Graph recurring event id is missing.");
    }

    private async Task<List<OutlookSeriesInstance>> GetSeriesInstancesAsync(
        string masterId,
        string accessToken,
        DateTime startAt,
        DateTime endAt)
    {
        var url = $"https://graph.microsoft.com/v1.0/me/events/{masterId}/instances" +
            $"?startDateTime={Uri.EscapeDataString(startAt.ToString("o"))}" +
            $"&endDateTime={Uri.EscapeDataString(endAt.ToString("o"))}" +
            "&$select=id,start&$top=999";
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        var response = await _httpClient.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException($"Microsoft recurring event instances error: {body}");

        using var doc = JsonDocument.Parse(body);
        return doc.RootElement.GetProperty("value")
            .EnumerateArray()
            .Select(item => new OutlookSeriesInstance
            {
                Id = item.GetProperty("id").GetString() ?? string.Empty,
                StartAt = ReadGraphDateTime(item.GetProperty("start"))
            })
            .Where(item => !string.IsNullOrWhiteSpace(item.Id))
            .OrderBy(item => item.StartAt)
            .ToList();
    }

    private async Task<List<OutlookSeriesInstance>> GetSeriesInstancesWithRetryAsync(
        string masterId,
        string accessToken,
        DateTime startAt,
        DateTime endAt,
        int expectedCount)
    {
        Exception? lastError = null;

        for (var attempt = 1; attempt <= 5; attempt++)
        {
            try
            {
                var instances = await GetSeriesInstancesAsync(masterId, accessToken, startAt, endAt);
                if (instances.Count == expectedCount)
                    return instances;

                lastError = new InvalidOperationException(
                    $"Outlook returned {instances.Count} of {expectedCount} expected series occurrences.");
            }
            catch (Exception ex)
            {
                lastError = ex;
            }

            if (attempt < 5)
                await Task.Delay(TimeSpan.FromMilliseconds(500 * attempt));
        }

        throw new InvalidOperationException(
            $"Outlook did not make all recurring event occurrences available in time. {lastError?.Message}",
            lastError);
    }

    private async Task TryDeleteEventAsync(string eventId, string accessToken)
    {
        try
        {
            using var request = new HttpRequestMessage(
                HttpMethod.Delete,
                $"https://graph.microsoft.com/v1.0/me/events/{eventId}");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            await _httpClient.SendAsync(request);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not clean up Outlook series {EventId} after failed linking.", eventId);
        }
    }

    private Dictionary<string, object?> BuildGraphEventPayload(Session session, List<string> categories)
    {
        var clientName = string.Join(" + ",
             session.Participants.Select(p => $"{p.Client.FirstName} {p.Client.LastName}"));
        var trainerName = $"{session.Trainer.User.FirstName} {session.Trainer.User.LastName}";
        var locationName = session.Location.Name;

        var attendees = BuildAttendees(session);

        var payload = new Dictionary<string, object?>
        {
            ["subject"] = SessionTitleBuilder.BuildOutlookSubject(session),
            ["body"] = new
            {
                contentType = "HTML",
                content = $"""
                <p><strong>{(IsGroupClass(session) ? "Zapisani" : "Klient")}:</strong> {clientName}</p>
                <p><strong>Trener:</strong> {trainerName}</p>
                <p><strong>Lokalizacja:</strong> {locationName}</p>
                <p><strong>Sala:</strong> {(IsGroupClass(session) ? "zarezerwowana na wyłączność" : "zarezerwowana")}</p>
                <p><strong>Status:</strong> {session.Status}</p>
                <p><strong>Notatka:</strong> {session.Note}</p>
                """
            },
            ["start"] = new
            {
                dateTime = ToStudioLocalTime(session.StartAt).ToString("yyyy-MM-ddTHH:mm:ss"),
                timeZone = OutlookStudioTimeZone
            },
            ["end"] = new
            {
                dateTime = ToStudioLocalTime(session.EndAt).ToString("yyyy-MM-ddTHH:mm:ss"),
                timeZone = OutlookStudioTimeZone
            },
            ["location"] = new
            {
                displayName = locationName,
                locationEmailAddress = session.Location.CalendarEmail
            },
            ["attendees"] = attendees,
            ["showAs"] = "busy"
        };

        if (categories.Count > 0)
            payload["categories"] = categories;

        return payload;
    }

    private static DateTime ToStudioLocalTime(DateTime value)
    {
        if (value.Kind == DateTimeKind.Unspecified)
            return value;

        var utc = value.Kind == DateTimeKind.Utc
            ? value
            : value.ToUniversalTime();

        return TimeZoneInfo.ConvertTimeFromUtc(utc, GetStudioTimeZone());
    }

    private static TimeZoneInfo GetStudioTimeZone()
    {
        foreach (var id in new[] { "Europe/Warsaw", OutlookStudioTimeZone })
        {
            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById(id);
            }
            catch (TimeZoneNotFoundException)
            {
            }
            catch (InvalidTimeZoneException)
            {
            }
        }

        return TimeZoneInfo.Utc;
    }

    private static List<object> BuildAttendees(Session session)
    {
        var attendees = session.Participants
            .Where(p => !string.IsNullOrWhiteSpace(p.Client.Email))
            .OrderBy(p => p.Client.FirstName)
            .ThenBy(p => p.Client.LastName)
            .Select(p => new
            {
                emailAddress = new
                {
                    address = p.Client.Email,
                    name = $"{p.Client.FirstName} {p.Client.LastName}".Trim()
                },
                type = "required"
            })
            .Cast<object>()
            .ToList();

        if (!string.IsNullOrWhiteSpace(session.Location.CalendarEmail))
        {
            attendees.Add(new
            {
                emailAddress = new
                {
                    address = session.Location.CalendarEmail,
                    name = session.Location.Name
                },
                type = "resource"
            });
        }

        return attendees;
    }

    private async Task<CalendarIntegration?> GetTrainerIntegrationAsync(int userId)
    {
        return await _context.CalendarIntegrations
            .FirstOrDefaultAsync(x =>
                x.UserId == userId &&
                x.Provider == "Outlook" &&
                x.IsActive);
    }

    private async Task DeleteLinkedEventAsync(CalendarEventLink link)
    {
        var integration = link.CalendarIntegration
            ?? await _context.CalendarIntegrations
                .FirstOrDefaultAsync(x => x.Id == link.CalendarIntegrationId);

        if (integration is null || !integration.IsActive)
            return;

        await DeleteEventAsync(integration, link.ExternalEventId);
    }

    private async Task DeleteEventAsync(CalendarIntegration integration, string externalEventId)
    {
        await EnsureAccessTokenAsync(integration);

        using var request = new HttpRequestMessage(
            HttpMethod.Delete,
            $"https://graph.microsoft.com/v1.0/me/events/{externalEventId}");

        request.Headers.Authorization =
            new AuthenticationHeaderValue("Bearer", integration.AccessToken);

        var response = await _httpClient.SendAsync(request);

        if (response.IsSuccessStatusCode ||
            response.StatusCode is System.Net.HttpStatusCode.NotFound or System.Net.HttpStatusCode.Gone)
            return;

        var body = await response.Content.ReadAsStringAsync();
        throw new InvalidOperationException($"Microsoft delete event error: {body}");
    }

    private async Task UpsertExternalCalendarEventAsync(
        Session session,
        int calendarIntegrationId,
        string externalEventId,
        string? seriesMasterId = null,
        bool isRecurring = false)
    {
        var externalEvent = await _context.ExternalCalendarEvents
            .FirstOrDefaultAsync(x =>
                x.CalendarIntegrationId == calendarIntegrationId &&
                x.ExternalEventId == externalEventId);

        if (externalEvent is null)
        {
            externalEvent = new ExternalCalendarEvent
            {
                CalendarIntegrationId = calendarIntegrationId,
                Provider = "Outlook",
                ExternalEventId = externalEventId
            };

            await _context.ExternalCalendarEvents.AddAsync(externalEvent);
        }

        var categories = ResolveGraphEventCategories(session);
        var categoryColors = ResolveGraphEventCategoryColors(session, categories);

        externalEvent.Subject = session.Title;
        externalEvent.BodyPreview = session.Note;
        externalEvent.StartAt = session.StartAt;
        externalEvent.EndAt = session.EndAt;
        externalEvent.LocationName = session.Location.Name;
        externalEvent.LocationEmail = session.Location.CalendarEmail;
        externalEvent.AttendeesJson = JsonSerializer.Serialize(
            session.Participants
                .Select(p => p.Client.Email.Trim().ToLowerInvariant())
                .Where(e => !string.IsNullOrWhiteSpace(e))
                .Distinct()
                .ToList());
        externalEvent.CategoriesJson = JsonSerializer.Serialize(categories);
        externalEvent.CategoryColorsJson = JsonSerializer.Serialize(categoryColors);
        externalEvent.SessionId = session.Id;
        externalEvent.IsConvertedToSession = true;
        externalEvent.SeriesMasterId = seriesMasterId;
        externalEvent.IsRecurring = isRecurring;
        externalEvent.ImportedAt = DateTime.UtcNow;

        session.OutlookCategoryColorsJson = JsonSerializer.Serialize(categoryColors);
    }

    private async Task EnsureTrainerMasterCategoryAsync(
        Session session,
        string accessToken,
        List<string> categories)
    {
        var trainerCategoryName = NormalizeCategoryName(session.Trainer.OutlookCategoryName);
        var managedCategories = new List<(string Name, string Color)>();
        if (IsGroupClass(session) && categories.Contains(
                SessionTitleBuilder.GroupClassOutlookCategory,
                StringComparer.OrdinalIgnoreCase))
        {
            managedCategories.Add((SessionTitleBuilder.GroupClassOutlookCategory, "preset0"));
        }
        if (trainerCategoryName is not null &&
            categories.Contains(trainerCategoryName, StringComparer.OrdinalIgnoreCase))
        {
            managedCategories.Add((trainerCategoryName, NormalizeCategoryColor(session.Trainer.OutlookCategoryColor)));
        }

        if (managedCategories.Count == 0)
            return;

        try
        {
            using var listRequest = new HttpRequestMessage(
                HttpMethod.Get,
                "https://graph.microsoft.com/v1.0/me/outlook/masterCategories?$select=displayName,color");

            listRequest.Headers.Authorization =
                new AuthenticationHeaderValue("Bearer", accessToken);

            var listResponse = await _httpClient.SendAsync(listRequest);
            var listBody = await listResponse.Content.ReadAsStringAsync();

            if (!listResponse.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "Could not read Outlook master categories for trainer {TrainerId}: {Body}",
                    session.TrainerId,
                    listBody);

                return;
            }

            using var doc = JsonDocument.Parse(listBody);

            var existingNames = doc.RootElement.TryGetProperty("value", out var value) &&
                value.ValueKind == JsonValueKind.Array
                ? value.EnumerateArray()
                    .Select(category => category.TryGetProperty("displayName", out var displayName)
                        ? displayName.GetString()
                        : null)
                    .Where(name => !string.IsNullOrWhiteSpace(name))
                    .Select(name => name!)
                    .ToHashSet(StringComparer.OrdinalIgnoreCase)
                : new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var category in managedCategories.Where(category => !existingNames.Contains(category.Name)))
                await CreateMasterCategoryAsync(accessToken, category.Name, category.Color);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Could not ensure Outlook master categories for trainer {TrainerId}.",
                session.TrainerId);
        }
    }

    private async Task CreateMasterCategoryAsync(
        string accessToken,
        string categoryName,
        string color)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            "https://graph.microsoft.com/v1.0/me/outlook/masterCategories");

        request.Headers.Authorization =
            new AuthenticationHeaderValue("Bearer", accessToken);

        request.Content = new StringContent(
            JsonSerializer.Serialize(new
            {
                displayName = categoryName,
                color
            }),
            Encoding.UTF8,
            "application/json");

        var response = await _httpClient.SendAsync(request);

        if (response.IsSuccessStatusCode)
            return;

        var body = await response.Content.ReadAsStringAsync();

        _logger.LogWarning(
            "Could not create Outlook master category {CategoryName}: {Body}",
            categoryName,
            body);
    }

    private static List<string> ResolveGraphEventCategories(Session session)
    {
        var categories = new List<string>();
        var trainerCategoryName = NormalizeCategoryName(session.Trainer.OutlookCategoryName);

        if (IsGroupClass(session))
            categories.Add(SessionTitleBuilder.GroupClassOutlookCategory);

        if (trainerCategoryName is not null)
            categories.Add(trainerCategoryName);

        categories.AddRange(ReadStringList(session.OutlookCategoriesJson));

        return categories
            .Select(NormalizeCategoryName)
            .Where(c => c is not null)
            .Select(c => c!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static List<OutlookCategoryColor> ResolveGraphEventCategoryColors(
        Session session,
        List<string> categories)
    {
        return categories
            .Select(category => new OutlookCategoryColor
            {
                Name = category,
                Color = string.Equals(category, SessionTitleBuilder.GroupClassOutlookCategory, StringComparison.OrdinalIgnoreCase)
                    ? "preset0"
                    : string.Equals(category, session.Trainer.OutlookCategoryName, StringComparison.OrdinalIgnoreCase)
                    ? NormalizeCategoryColor(session.Trainer.OutlookCategoryColor)
                    : null
            })
            .ToList();
    }

    private static string? NormalizeCategoryName(string? value)
    {
        var normalized = value?.Trim();

        return string.IsNullOrWhiteSpace(normalized)
            ? null
            : normalized;
    }

    private static string NormalizeCategoryColor(string? value)
    {
        var normalized = value?.Trim();

        return string.IsNullOrWhiteSpace(normalized)
            ? "preset7"
            : normalized;
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

    private static bool IsGroupClass(Session session)
    {
        return session.IsPubliclyBookable ||
            string.Equals(session.PlannedSessionType, "Group", StringComparison.OrdinalIgnoreCase);
    }

    private static DateTime ReadGraphDateTime(JsonElement value)
    {
        var raw = value.GetProperty("dateTime").GetString()
            ?? throw new InvalidOperationException("Graph occurrence start is missing.");
        if (raw.EndsWith("Z", StringComparison.OrdinalIgnoreCase) &&
            DateTimeOffset.TryParse(raw, out var offset))
        {
            return offset.UtcDateTime;
        }

        if (!DateTime.TryParse(raw, out var parsed))
            throw new InvalidOperationException("Graph occurrence start is invalid.");

        var timeZone = value.TryGetProperty("timeZone", out var timeZoneElement)
            ? timeZoneElement.GetString()
            : null;
        if (string.Equals(timeZone, "UTC", StringComparison.OrdinalIgnoreCase))
            return DateTime.SpecifyKind(parsed, DateTimeKind.Utc);

        return TimeZoneInfo.ConvertTimeToUtc(
            DateTime.SpecifyKind(parsed, DateTimeKind.Unspecified),
            GetStudioTimeZone());
    }

    private sealed class OutlookCategoryColor
    {
        public string Name { get; set; } = string.Empty;

        public string? Color { get; set; }
    }

    private sealed class OutlookSeriesInstance
    {
        public string Id { get; set; } = string.Empty;

        public DateTime StartAt { get; set; }
    }

    private async Task EnsureAccessTokenAsync(CalendarIntegration integration)
    {
        if (integration.AccessTokenExpiresAt > DateTime.UtcNow.AddMinutes(2) &&
            !string.IsNullOrWhiteSpace(integration.AccessToken))
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(integration.RefreshToken))
            throw new InvalidOperationException("Outlook refresh token is missing.");

        var tokenUrl =
            $"https://login.microsoftonline.com/{_settings.TenantId}/oauth2/v2.0/token";

        var form = new Dictionary<string, string>
        {
            ["client_id"] = _settings.ClientId,
            ["client_secret"] = _settings.ClientSecret,
            ["refresh_token"] = integration.RefreshToken,
            ["grant_type"] = "refresh_token",
            ["scope"] = _settings.Scopes
        };

        var response = await _httpClient.PostAsync(
            tokenUrl,
            new FormUrlEncodedContent(form));

        var body = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException($"Microsoft refresh token error: {body}");

        using var doc = JsonDocument.Parse(body);
        var root = doc.RootElement;

        integration.AccessToken =
            root.GetProperty("access_token").GetString() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(integration.AccessToken))
            throw new InvalidOperationException("Microsoft returned empty access token.");

        if (root.TryGetProperty("refresh_token", out var refresh))
        {
            var newRefreshToken = refresh.GetString();

            if (!string.IsNullOrWhiteSpace(newRefreshToken))
                integration.RefreshToken = newRefreshToken;
        }

        integration.AccessTokenExpiresAt = DateTime.UtcNow.AddSeconds(
            root.GetProperty("expires_in").GetInt32() - 60);

        await _context.SaveChangesAsync();
    }
}
