using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using StudioCRM.Application.Interfaces.Calendar;
using StudioCRM.Domain.Entities;
using StudioCRM.Infrastructure.Persistence;

namespace StudioCRM.Infrastructure.Services.Calendar;

public class OutlookWebhookService : IOutlookWebhookService
{
    private readonly StudioCRMDbContext _context;
    private readonly HttpClient _httpClient;
    private readonly IOutlookTokenService _tokenService;
    private readonly IConfiguration _configuration;

    public OutlookWebhookService(
        StudioCRMDbContext context,
        HttpClient httpClient,
        IConfiguration configuration,
        IOutlookTokenService tokenService)
    {
        _context = context;
        _httpClient = httpClient;
        _configuration = configuration;
        _tokenService = tokenService;
    }

    public async Task HandleNotificationAsync(string requestBody)
    {
        using var doc = JsonDocument.Parse(requestBody);

        if (!doc.RootElement.TryGetProperty("value", out var value))
            return;

        foreach (var notification in value.EnumerateArray())
        {
            var subscriptionId = notification.GetProperty("subscriptionId").GetString();

            if (string.IsNullOrWhiteSpace(subscriptionId))
                continue;

            var subscription = await _context.CalendarSubscriptions
                .Include(x => x.CalendarIntegration)
                .FirstOrDefaultAsync(x =>
                    x.SubscriptionId == subscriptionId &&
                    x.IsActive);

            if (subscription is null)
                continue;

            var integration = subscription.CalendarIntegration;

            var resource = notification.GetProperty("resource").GetString();

            if (string.IsNullOrWhiteSpace(resource))
                continue;

            var externalEventId = resource.Split('/').LastOrDefault();

            if (string.IsNullOrWhiteSpace(externalEventId))
                continue;

            var changeType = notification.GetProperty("changeType").GetString();

            if (changeType == "deleted")
            {
                await MarkDeletedAsync(integration.Id, externalEventId);
                continue;
            }

            await ImportOrUpdateEventAsync(integration, externalEventId);
        }
    }

    private async Task ImportOrUpdateEventAsync(CalendarIntegration integration, string externalEventId)
    {
        await _tokenService.EnsureValidAccessTokenAsync(integration);

        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            $"https://graph.microsoft.com/v1.0/me/events/{externalEventId}?$select=id,subject,bodyPreview,start,end,location,organizer,attendees,type,seriesMasterId");

        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", integration.AccessToken);

        var response = await _httpClient.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
            return;

        using var doc = JsonDocument.Parse(body);
        var root = doc.RootElement;

        var existing = await _context.ExternalCalendarEvents
            .FirstOrDefaultAsync(x =>
                x.CalendarIntegrationId == integration.Id &&
                x.ExternalEventId == externalEventId);

        if (existing is null)
        {
            existing = new ExternalCalendarEvent
            {
                CalendarIntegrationId = integration.Id,
                Provider = "Outlook",
                ExternalEventId = externalEventId,
                ImportedAt = DateTime.UtcNow
            };

            await _context.ExternalCalendarEvents.AddAsync(existing);
        }

        existing.Subject = root.TryGetProperty("subject", out var subject)
            ? subject.GetString() ?? string.Empty
            : string.Empty;

        existing.BodyPreview = root.TryGetProperty("bodyPreview", out var preview)
            ? preview.GetString()
            : null;

        existing.LocationName = root.TryGetProperty("location", out var location) &&
                                location.TryGetProperty("displayName", out var displayName)
            ? displayName.GetString()
            : null;

        existing.OrganizerEmail = root.TryGetProperty("organizer", out var organizer) &&
                                  organizer.TryGetProperty("emailAddress", out var organizerEmailAddress) &&
                                  organizerEmailAddress.TryGetProperty("address", out var organizerAddress)
            ? organizerAddress.GetString()
            : null;

        existing.StartAt = ReadGraphDateTime(root, "start");
        existing.EndAt = ReadGraphDateTime(root, "end");

        var attendeeEmails = ReadAttendeeEmails(root);
        existing.AttendeesJson = JsonSerializer.Serialize(attendeeEmails);

        existing.LocationEmail = await ResolveLocationEmailAsync(attendeeEmails, existing.LocationName);

        existing.SeriesMasterId = root.TryGetProperty("seriesMasterId", out var seriesMasterId)
            ? seriesMasterId.GetString()
            : null;

        existing.IsRecurring = root.TryGetProperty("type", out var type) &&
                               !string.Equals(type.GetString(), "singleInstance", StringComparison.OrdinalIgnoreCase);

        existing.ImportedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();
        
        if (!existing.IsConvertedToSession)
        {
            var mapper = new OutlookEventMapperService(_context);
            await mapper.MapToSessionAsync(existing);
        }
    }

    private async Task<string?> ResolveLocationEmailAsync(List<string> attendeeEmails, string? locationName)
    {
        var locationEmails = await _context.Locations
            .Where(l => l.IsActive && l.CalendarEmail != null)
            .Select(l => l.CalendarEmail!)
            .ToListAsync();

        var normalizedLocationEmails = locationEmails
            .Select(x => x.Trim().ToLowerInvariant())
            .ToList();

        var locationFromAttendees = attendeeEmails
            .Select(x => x.Trim().ToLowerInvariant())
            .FirstOrDefault(x => normalizedLocationEmails.Contains(x));

        if (!string.IsNullOrWhiteSpace(locationFromAttendees))
            return locationFromAttendees;

        if (string.IsNullOrWhiteSpace(locationName))
            return null;

        var normalizedLocationName = locationName.Trim().ToLowerInvariant();

        var location = await _context.Locations
            .FirstOrDefaultAsync(l =>
                l.IsActive &&
                (
                    normalizedLocationName.Contains(l.Name.ToLower()) ||
                    l.Name.ToLower().Contains(normalizedLocationName)
                ));

        return location?.CalendarEmail;
    }

    private static List<string> ReadAttendeeEmails(JsonElement root)
    {
        var emails = new List<string>();

        if (!root.TryGetProperty("attendees", out var attendees) ||
            attendees.ValueKind != JsonValueKind.Array)
        {
            return emails;
        }

        foreach (var attendee in attendees.EnumerateArray())
        {
            if (!attendee.TryGetProperty("emailAddress", out var emailAddress))
                continue;

            if (!emailAddress.TryGetProperty("address", out var address))
                continue;

            var email = address.GetString();

            if (!string.IsNullOrWhiteSpace(email))
                emails.Add(email.Trim().ToLowerInvariant());
        }

        return emails.Distinct().ToList();
    }

    private async Task MarkDeletedAsync(int integrationId, string externalEventId)
    {
        var existing = await _context.ExternalCalendarEvents
            .FirstOrDefaultAsync(x =>
                x.CalendarIntegrationId == integrationId &&
                x.ExternalEventId == externalEventId);

        if (existing is null)
            return;

        existing.Subject = "[DELETED] " + existing.Subject;
        await _context.SaveChangesAsync();
    }

    private static DateTime ReadGraphDateTime(JsonElement root, string propertyName)
    {
        if (!root.TryGetProperty(propertyName, out var dateWrapper))
            return DateTime.UtcNow;

        if (!dateWrapper.TryGetProperty("dateTime", out var dateTime))
            return DateTime.UtcNow;

        var raw = dateTime.GetString();

        return DateTime.TryParse(raw, out var parsed)
            ? DateTime.SpecifyKind(parsed, DateTimeKind.Utc)
            : DateTime.UtcNow;
    }
}