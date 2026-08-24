using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using StudioCRM.Application.Interfaces.Calendar;
using StudioCRM.Domain.Entities;
using StudioCRM.Infrastructure.Persistence;

namespace StudioCRM.Infrastructure.Services.Calendar;

public class OutlookWebhookService : IOutlookWebhookService
{
    private const string OutlookStudioTimeZone = "Central European Standard Time";

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
        if (string.IsNullOrWhiteSpace(requestBody))
            return;

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

            var externalEventId = ResolveExternalEventId(notification, resource);

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
            $"https://graph.microsoft.com/v1.0/me/events/{externalEventId}?$select=id,subject,bodyPreview,start,end,location,organizer,attendees,type,seriesMasterId,categories");

        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", integration.AccessToken);

        var response = await _httpClient.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();

        if (response.StatusCode is System.Net.HttpStatusCode.NotFound or System.Net.HttpStatusCode.Gone)
        {
            await MarkDeletedAsync(integration.Id, externalEventId);
            return;
        }

        if (!response.IsSuccessStatusCode)
            return;

        using var doc = JsonDocument.Parse(body);
        var root = doc.RootElement;

        var isRecurring = root.TryGetProperty("type", out var type) &&
                          !string.Equals(type.GetString(), "singleInstance", StringComparison.OrdinalIgnoreCase);

        var seriesMasterId = root.TryGetProperty("seriesMasterId", out var seriesMasterIdElement)
            ? seriesMasterIdElement.GetString()
            : null;

        var isSeriesMaster = root.TryGetProperty("type", out var typeForMaster) &&
                             string.Equals(typeForMaster.GetString(), "seriesMaster", StringComparison.OrdinalIgnoreCase);

        if (isSeriesMaster)
        {
            var instances = await GetEventInstancesAsync(
                integration,
                externalEventId,
                DateTime.UtcNow.AddDays(-14),
                DateTime.UtcNow.AddMonths(3));

            foreach (var instanceId in instances)
            {
                if (!string.IsNullOrWhiteSpace(instanceId) && instanceId != externalEventId)
                    await ImportOrUpdateEventAsync(integration, instanceId);
            }

            return;
        }

        var subjectValue = root.TryGetProperty("subject", out var subject)
            ? subject.GetString() ?? string.Empty
            : string.Empty;

        var bodyPreviewValue = root.TryGetProperty("bodyPreview", out var preview)
            ? preview.GetString()
            : null;

        var locationNameValue = root.TryGetProperty("location", out var location) &&
                                location.TryGetProperty("displayName", out var displayName)
            ? displayName.GetString()
            : null;

        var locationEmailValue = ReadLocationEmail(root);

        var organizerEmailValue = root.TryGetProperty("organizer", out var organizer) &&
                                  organizer.TryGetProperty("emailAddress", out var organizerEmailAddress) &&
                                  organizerEmailAddress.TryGetProperty("address", out var organizerAddress)
            ? organizerAddress.GetString()
            : null;

        var startAtValue = ReadGraphDateTime(root, "start");
        var endAtValue = ReadGraphDateTime(root, "end");

        var attendeeEmails = ReadAttendeeEmails(root);
        var categories = ReadCategories(root);
        var categoryColors = await ResolveCategoryColorsAsync(integration, categories);

        var resolvedLocationEmail = await ResolveLocationEmailAsync(
            attendeeEmails,
            locationNameValue,
            locationEmailValue);

        var isKnownLocation = await IsKnownCrmLocationAsync(
            locationNameValue,
            resolvedLocationEmail);

        if (!isKnownLocation)
            return;

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

        existing.Subject = subjectValue;
        existing.BodyPreview = bodyPreviewValue;
        existing.LocationName = locationNameValue;
        existing.LocationEmail = resolvedLocationEmail;
        existing.OrganizerEmail = organizerEmailValue;
        existing.StartAt = startAtValue;
        existing.EndAt = endAtValue;
        existing.AttendeesJson = JsonSerializer.Serialize(attendeeEmails);
        existing.CategoriesJson = JsonSerializer.Serialize(categories);
        existing.CategoryColorsJson = JsonSerializer.Serialize(categoryColors);
        existing.SeriesMasterId = seriesMasterId;
        existing.IsRecurring = isRecurring;
        existing.ImportedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        if (existing.SessionId != null)
        {
            await UpdateSessionFromOutlookAsync(
                integration,
                existing,
                externalEventId,
                subjectValue);
        }
        else if (!existing.IsConvertedToSession)
        {
            var mapper = new OutlookEventMapperService(_context);
            var (session, _) = await mapper.MapToSessionAsync(existing);

            if (session != null)
            {
                await UpdateOutlookEventTitleIfNeededAsync(
                    integration,
                    externalEventId,
                    currentOutlookTitle: subjectValue,
                    newTitle: session.Title);
            }
        }
    }

    private async Task<List<string>> GetEventInstancesAsync(
        CalendarIntegration integration,
        string eventId,
        DateTime start,
        DateTime end)
    {
        await _tokenService.EnsureValidAccessTokenAsync(integration);

        var url =
            $"https://graph.microsoft.com/v1.0/me/events/{eventId}/instances" +
            $"?startDateTime={Uri.EscapeDataString(start.ToString("o"))}" +
            $"&endDateTime={Uri.EscapeDataString(end.ToString("o"))}" +
            "&$select=id";

        using var request = new HttpRequestMessage(HttpMethod.Get, url);

        request.Headers.Authorization =
            new AuthenticationHeaderValue("Bearer", integration.AccessToken);

        var response = await _httpClient.SendAsync(request);

        if (!response.IsSuccessStatusCode)
            return new List<string>();

        var body = await response.Content.ReadAsStringAsync();

        using var doc = JsonDocument.Parse(body);

        if (!doc.RootElement.TryGetProperty("value", out var value))
            return new List<string>();

        return value.EnumerateArray()
            .Select(x => x.TryGetProperty("id", out var id) ? id.GetString() : null)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x!)
            .Distinct()
            .ToList();
    }

    private async Task UpdateSessionFromOutlookAsync(
        CalendarIntegration integration,
        ExternalCalendarEvent evt,
        string externalEventId,
        string currentOutlookTitle)
    {
        var session = await _context.Sessions
            .FirstOrDefaultAsync(s => s.Id == evt.SessionId);

        if (session == null)
            return;

        session.StartAt = evt.StartAt;
        session.EndAt = evt.EndAt;

        var location = await FindLocationFromOutlookAsync(evt);

        if (location != null)
        {
            session.LocationId = location.Id;
        }

        session.OutlookCategoriesJson = evt.CategoriesJson;
        session.OutlookCategoryColorsJson = evt.CategoryColorsJson;
        session.PrimaryOutlookCategory = GetPrimaryCategory(evt.CategoriesJson);

        await SyncSessionParticipantsFromOutlookAsync(session, evt);

        var newTitle = await BuildSessionTitleFromParticipantsAsync(session.Id);

        session.Title = newTitle;
        session.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        await UpdateOutlookEventTitleIfNeededAsync(
            integration,
            externalEventId,
            currentOutlookTitle,
            newTitle);
    }

    private async Task SyncSessionParticipantsFromOutlookAsync(Session session, ExternalCalendarEvent evt)
    {
        var attendeeEmails = ReadAttendeeEmailsFromJson(evt.AttendeesJson)
            .Select(x => x.Trim().ToLowerInvariant())
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct()
            .ToList();

        var locationEmails = await _context.Locations
            .Where(l => l.CalendarEmail != null)
            .Select(l => l.CalendarEmail!.ToLower())
            .ToListAsync();

        var organizerEmail = (evt.OrganizerEmail ?? string.Empty)
            .Trim()
            .ToLowerInvariant();

        attendeeEmails = attendeeEmails
            .Where(email =>
                email != organizerEmail &&
                !locationEmails.Contains(email))
            .ToList();

        var clients = await _context.Clients
            .Where(c =>
                !c.IsDeleted &&
                attendeeEmails.Contains(c.Email.ToLower()))
            .ToListAsync();

        var desiredClientIds = clients
            .Select(c => c.Id)
            .ToHashSet();

        var currentParticipants = await _context.SessionParticipants
            .Where(p => p.SessionId == session.Id)
            .ToListAsync();

        var currentClientIds = currentParticipants
            .Select(p => p.ClientId)
            .ToHashSet();

        var clientsToAdd = clients
            .Where(c => !currentClientIds.Contains(c.Id))
            .ToList();

        foreach (var client in clientsToAdd)
        {
            var activeClientPackage = await _context.ClientPackages
                .Where(cp => cp.ClientId == client.Id && cp.IsActive)
                .OrderByDescending(cp => cp.PurchaseDate)
                .FirstOrDefaultAsync();

            await _context.SessionParticipants.AddAsync(new SessionParticipant
            {
                SessionId = session.Id,
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

        var canRemoveParticipants = !string.Equals(session.Status, "Completed", StringComparison.OrdinalIgnoreCase);

        var participantsToRemove = currentParticipants
            .Where(p =>
                canRemoveParticipants &&
                !p.IsCountedFromPackage &&
                !desiredClientIds.Contains(p.ClientId))
            .ToList();

        _context.SessionParticipants.RemoveRange(participantsToRemove);

        await _context.SaveChangesAsync();
    }

    private async Task<string> BuildSessionTitleFromParticipantsAsync(int sessionId)
    {
        var clients = await _context.SessionParticipants
            .Where(p => p.SessionId == sessionId)
            .Include(p => p.Client)
            .Select(p => p.Client)
            .ToListAsync();

        if (clients.Count == 0)
            return "Trening";

        return string.Join(" + ", clients
            .OrderBy(c => c.FirstName)
            .ThenBy(c => c.LastName)
            .Select(c =>
            {
                var lastInitial = string.IsNullOrWhiteSpace(c.LastName)
                    ? string.Empty
                    : $"{c.LastName[0]}";

                return $"{c.FirstName} {lastInitial}".Trim();
            }));
    }

    private async Task<Location?> FindLocationFromOutlookAsync(ExternalCalendarEvent evt)
    {
        var locationEmail = (evt.LocationEmail ?? string.Empty)
            .Trim()
            .ToLowerInvariant();

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

        var locationName = (evt.LocationName ?? string.Empty)
            .Trim()
            .ToLowerInvariant();

        if (string.IsNullOrWhiteSpace(locationName))
            return null;

        return await _context.Locations
            .FirstOrDefaultAsync(l =>
                l.IsActive &&
                (
                    locationName.Contains(l.Name.ToLower()) ||
                    l.Name.ToLower().Contains(locationName)
                ));
    }

    private async Task<bool> IsKnownCrmLocationAsync(string? locationName, string? locationEmail)
    {
        var normalizedLocationName = (locationName ?? string.Empty)
            .Trim()
            .ToLowerInvariant();

        var normalizedLocationEmail = (locationEmail ?? string.Empty)
            .Trim()
            .ToLowerInvariant();

        return await _context.Locations
            .AnyAsync(l =>
                l.IsActive &&
                (
                    (!string.IsNullOrWhiteSpace(normalizedLocationEmail) &&
                     l.CalendarEmail != null &&
                     l.CalendarEmail.ToLower() == normalizedLocationEmail)
                    ||
                    (!string.IsNullOrWhiteSpace(normalizedLocationName) &&
                     normalizedLocationName.Contains(l.Name.ToLower()))
                ));
    }

    private async Task UpdateOutlookEventTitleIfNeededAsync(
        CalendarIntegration integration,
        string externalEventId,
        string currentOutlookTitle,
        string newTitle)
    {
        if (string.IsNullOrWhiteSpace(newTitle))
            return;

        if (AreTitlesEqual(currentOutlookTitle, newTitle))
            return;

        await _tokenService.EnsureValidAccessTokenAsync(integration);

        using var request = new HttpRequestMessage(
            HttpMethod.Patch,
            $"https://graph.microsoft.com/v1.0/me/events/{externalEventId}");

        request.Headers.Authorization =
            new AuthenticationHeaderValue("Bearer", integration.AccessToken);

        request.Content = new StringContent(
            JsonSerializer.Serialize(new
            {
                subject = newTitle
            }),
            Encoding.UTF8,
            "application/json");

        await _httpClient.SendAsync(request);
    }

    private static bool AreTitlesEqual(string? currentTitle, string? newTitle)
    {
        var current = (currentTitle ?? string.Empty).Trim();
        var next = (newTitle ?? string.Empty).Trim();

        return string.Equals(current, next, StringComparison.OrdinalIgnoreCase);
    }

    private async Task<string?> ResolveLocationEmailAsync(
        List<string> attendeeEmails,
        string? locationName,
        string? locationEmailFromGraph)
    {
        if (!string.IsNullOrWhiteSpace(locationEmailFromGraph))
        {
            var normalizedGraphLocationEmail = locationEmailFromGraph.Trim().ToLowerInvariant();

            var exists = await _context.Locations.AnyAsync(l =>
                l.IsActive &&
                l.CalendarEmail != null &&
                l.CalendarEmail.ToLower() == normalizedGraphLocationEmail);

            if (exists)
                return normalizedGraphLocationEmail;
        }

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

    private static string? ReadLocationEmail(JsonElement root)
    {
        if (!root.TryGetProperty("location", out var location) ||
            location.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        if (location.TryGetProperty("locationEmailAddress", out var locationEmailAddress))
        {
            var value = locationEmailAddress.GetString();

            if (!string.IsNullOrWhiteSpace(value))
                return value.Trim().ToLowerInvariant();
        }

        if (location.TryGetProperty("emailAddress", out var emailAddress))
        {
            var value = emailAddress.GetString();

            if (!string.IsNullOrWhiteSpace(value))
                return value.Trim().ToLowerInvariant();
        }

        return null;
    }

    private static List<string> ReadCategories(JsonElement root)
    {
        var categories = new List<string>();

        if (!root.TryGetProperty("categories", out var categoriesElement) ||
            categoriesElement.ValueKind != JsonValueKind.Array)
        {
            return categories;
        }

        foreach (var category in categoriesElement.EnumerateArray())
        {
            var value = category.GetString();

            if (!string.IsNullOrWhiteSpace(value))
                categories.Add(value.Trim());
        }

        return categories.Distinct().ToList();
    }

    private async Task<List<OutlookCategoryColor>> ResolveCategoryColorsAsync(
        CalendarIntegration integration,
        List<string> categories)
    {
        if (categories.Count == 0)
            return new List<OutlookCategoryColor>();

        var masterCategoryColors = await ReadMasterCategoryColorsAsync(integration);

        return categories
            .Select(category => new OutlookCategoryColor
            {
                Name = category,
                Color = masterCategoryColors.TryGetValue(category, out var color) ? color : null
            })
            .ToList();
    }

    private async Task<Dictionary<string, string>> ReadMasterCategoryColorsAsync(
        CalendarIntegration integration)
    {
        await _tokenService.EnsureValidAccessTokenAsync(integration);

        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            "https://graph.microsoft.com/v1.0/me/outlook/masterCategories?$select=displayName,color");

        request.Headers.Authorization =
            new AuthenticationHeaderValue("Bearer", integration.AccessToken);

        var response = await _httpClient.SendAsync(request);

        if (!response.IsSuccessStatusCode)
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        var body = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(body);

        if (!doc.RootElement.TryGetProperty("value", out var value) ||
            value.ValueKind != JsonValueKind.Array)
        {
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }

        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var category in value.EnumerateArray())
        {
            var displayName = category.TryGetProperty("displayName", out var displayNameElement)
                ? displayNameElement.GetString()
                : null;

            var color = category.TryGetProperty("color", out var colorElement)
                ? colorElement.GetString()
                : null;

            if (!string.IsNullOrWhiteSpace(displayName) &&
                !string.IsNullOrWhiteSpace(color))
            {
                result[displayName.Trim()] = color.Trim();
            }
        }

        return result;
    }

    private static List<string> ReadAttendeeEmailsFromJson(string? attendeesJson)
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

    private async Task MarkDeletedAsync(int integrationId, string externalEventId)
    {
        var existing = await _context.ExternalCalendarEvents
            .FirstOrDefaultAsync(x =>
                x.CalendarIntegrationId == integrationId &&
                x.ExternalEventId == externalEventId);

        var deletedEvents = new List<ExternalCalendarEvent>();

        if (existing is not null)
            deletedEvents.Add(existing);

        var seriesEvents = await _context.ExternalCalendarEvents
            .Where(x =>
                x.CalendarIntegrationId == integrationId &&
                x.Provider == "Outlook" &&
                x.SeriesMasterId == externalEventId)
            .ToListAsync();

        deletedEvents.AddRange(seriesEvents);
        deletedEvents = deletedEvents
            .DistinctBy(x => x.Id)
            .ToList();

        var deletedExternalEventIds = deletedEvents
            .Select(x => x.ExternalEventId)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        deletedExternalEventIds.Add(externalEventId);
        deletedExternalEventIds = deletedExternalEventIds
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var sessionIds = deletedEvents
            .Where(x => x.SessionId.HasValue)
            .Select(x => x.SessionId!.Value)
            .Distinct()
            .ToList();

        var links = await _context.CalendarEventLinks
            .Where(x =>
                x.CalendarIntegrationId == integrationId &&
                x.Provider == "Outlook" &&
                deletedExternalEventIds.Contains(x.ExternalEventId))
            .ToListAsync();

        sessionIds.AddRange(links.Select(x => x.SessionId));
        sessionIds = sessionIds
            .Distinct()
            .ToList();

        foreach (var deletedEvent in deletedEvents)
        {
            if (!deletedEvent.Subject.StartsWith("[DELETED]", StringComparison.OrdinalIgnoreCase))
                deletedEvent.Subject = "[DELETED] " + deletedEvent.Subject;
        }

        if (sessionIds.Count > 0)
        {
            var sessions = await _context.Sessions
                .Where(s => sessionIds.Contains(s.Id))
                .ToListAsync();

            foreach (var session in sessions)
            {
                if (string.Equals(session.Status, "Completed", StringComparison.OrdinalIgnoreCase))
                    continue;

                session.Status = "Cancelled";
                session.UpdatedAt = DateTime.UtcNow;
            }
        }

        _context.CalendarEventLinks.RemoveRange(links);

        await _context.SaveChangesAsync();
    }

    private static string? ResolveExternalEventId(JsonElement notification, string resource)
    {
        if (notification.TryGetProperty("resourceData", out var resourceData) &&
            resourceData.ValueKind == JsonValueKind.Object &&
            resourceData.TryGetProperty("id", out var resourceId))
        {
            var value = resourceId.GetString();

            if (!string.IsNullOrWhiteSpace(value))
                return value.Trim();
        }

        var resourceIdFromPath = resource
            .Split('/', StringSplitOptions.RemoveEmptyEntries)
            .LastOrDefault();

        return string.IsNullOrWhiteSpace(resourceIdFromPath)
            ? null
            : Uri.UnescapeDataString(resourceIdFromPath.Trim());
    }

    private static DateTime ReadGraphDateTime(JsonElement root, string propertyName)
    {
        if (!root.TryGetProperty(propertyName, out var dateWrapper))
            return DateTime.UtcNow;

        if (!dateWrapper.TryGetProperty("dateTime", out var dateTime))
            return DateTime.UtcNow;

        var raw = dateTime.GetString();

        if (!DateTime.TryParse(raw, out var parsed))
            return DateTime.UtcNow;

        var hasExplicitOffset =
            raw?.EndsWith("Z", StringComparison.OrdinalIgnoreCase) == true ||
            raw?.Contains('+') == true ||
            (raw?.LastIndexOf('-') ?? -1) > "yyyy-MM-dd".Length;

        if (hasExplicitOffset && DateTimeOffset.TryParse(raw, out var offsetValue))
            return offsetValue.UtcDateTime;

        var graphTimeZone = dateWrapper.TryGetProperty("timeZone", out var timeZoneElement)
            ? timeZoneElement.GetString()
            : null;

        if (string.Equals(graphTimeZone, "UTC", StringComparison.OrdinalIgnoreCase))
            return DateTime.SpecifyKind(parsed, DateTimeKind.Utc);

        var localTime = DateTime.SpecifyKind(parsed, DateTimeKind.Unspecified);
        return TimeZoneInfo.ConvertTimeToUtc(localTime, ResolveGraphTimeZone(graphTimeZone));
    }

    private static TimeZoneInfo ResolveGraphTimeZone(string? graphTimeZone)
    {
        var ids = new List<string>();

        if (!string.IsNullOrWhiteSpace(graphTimeZone))
            ids.Add(graphTimeZone);

        if (string.Equals(graphTimeZone, OutlookStudioTimeZone, StringComparison.OrdinalIgnoreCase))
            ids.Add("Europe/Warsaw");
        else if (string.Equals(graphTimeZone, "Europe/Warsaw", StringComparison.OrdinalIgnoreCase))
            ids.Add(OutlookStudioTimeZone);

        ids.Add("Europe/Warsaw");
        ids.Add(OutlookStudioTimeZone);

        foreach (var id in ids.Distinct(StringComparer.OrdinalIgnoreCase))
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

    private sealed class OutlookCategoryColor
    {
        public string Name { get; set; } = string.Empty;

        public string? Color { get; set; }
    }
}
