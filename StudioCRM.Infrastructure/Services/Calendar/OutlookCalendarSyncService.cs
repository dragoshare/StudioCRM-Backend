using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
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

    public OutlookCalendarSyncService(
        StudioCRMDbContext context,
        IOptions<OutlookSettings> options,
        HttpClient httpClient)
    {
        _context = context;
        _settings = options.Value;
        _httpClient = httpClient;
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
                await UpdateEventAsync(session, integration.AccessToken, existingLink.ExternalEventId);
            }

            existingLink.SyncedAt = DateTime.UtcNow;

            await UpsertExternalCalendarEventAsync(session, integration.Id, existingLink.ExternalEventId);
        }

        await _context.SaveChangesAsync();
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

    private async Task<string> CreateEventAsync(Session session, string accessToken)
    {
        var payload = BuildGraphEventPayload(session);

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

    private async Task UpdateEventAsync(Session session, string accessToken, string eventId)
    {
        var payload = BuildGraphEventPayload(session);

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

        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException($"Microsoft update event error: {body}");
    }

    private Dictionary<string, object?> BuildGraphEventPayload(Session session)
    {
        var clientName = string.Join(" + ",
             session.Participants.Select(p => $"{p.Client.FirstName} {p.Client.LastName}"));
        var trainerName = $"{session.Trainer.User.FirstName} {session.Trainer.User.LastName}";
        var locationName = session.Location.Name;

        var attendees = BuildAttendees(session);
        var categories = ReadStringList(session.OutlookCategoriesJson);

        var payload = new Dictionary<string, object?>
        {
            ["subject"] = $"StudioCRM: {session.Title} - {clientName}",
            ["body"] = new
            {
                contentType = "HTML",
                content = $"""
                <p><strong>Klient:</strong> {clientName}</p>
                <p><strong>Trener:</strong> {trainerName}</p>
                <p><strong>Lokalizacja:</strong> {locationName}</p>
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
            ["attendees"] = attendees
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

        await EnsureAccessTokenAsync(integration);

        using var request = new HttpRequestMessage(
            HttpMethod.Delete,
            $"https://graph.microsoft.com/v1.0/me/events/{link.ExternalEventId}");

        request.Headers.Authorization =
            new AuthenticationHeaderValue("Bearer", integration.AccessToken);

        var response = await _httpClient.SendAsync(request);

        if (response.IsSuccessStatusCode || response.StatusCode == System.Net.HttpStatusCode.NotFound)
            return;

        var body = await response.Content.ReadAsStringAsync();
        throw new InvalidOperationException($"Microsoft delete event error: {body}");
    }

    private async Task UpsertExternalCalendarEventAsync(
        Session session,
        int calendarIntegrationId,
        string externalEventId)
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
        externalEvent.CategoriesJson = session.OutlookCategoriesJson;
        externalEvent.SessionId = session.Id;
        externalEvent.IsConvertedToSession = true;
        externalEvent.ImportedAt = DateTime.UtcNow;
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
