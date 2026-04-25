using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using StudioCRM.Application.Interfaces;
using StudioCRM.Application.Interfaces.Calendar;
using StudioCRM.Domain.Entities;
using StudioCRM.Infrastructure.Persistence;

namespace StudioCRM.Infrastructure.Services;

public class OutlookSubscriptionService : IOutlookSubscriptionService
{
    private readonly StudioCRMDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly IOutlookCalendarSyncService _syncService;
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;

    public OutlookSubscriptionService(
        StudioCRMDbContext context,
        ICurrentUserService currentUser,
        IOutlookCalendarSyncService syncService,
        HttpClient httpClient,
        IConfiguration configuration)
    {
        _context = context;
        _currentUser = currentUser;
        _syncService = syncService;
        _httpClient = httpClient;
        _configuration = configuration;
    }

    public async Task CreateSubscriptionAsync()
    {
        if (!_currentUser.UserId.HasValue)
            throw new InvalidOperationException("User is not authenticated.");

        var integration = await _context.CalendarIntegrations
            .FirstOrDefaultAsync(x =>
                x.UserId == _currentUser.UserId.Value &&
                x.Provider == "Outlook" &&
                x.IsActive);

        if (integration is null)
            throw new InvalidOperationException("Outlook is not connected.");

        await EnsureAccessTokenForIntegrationAsync(integration);

        var webhookUrl = _configuration["Outlook:WebhookUrl"];

        if (string.IsNullOrWhiteSpace(webhookUrl))
            throw new InvalidOperationException("Outlook webhook URL is not configured.");

        var expiresAt = DateTime.UtcNow.AddHours(48);

        var payload = new
        {
            changeType = "created,updated,deleted",
            notificationUrl = webhookUrl,
            resource = "me/events",
            expirationDateTime = expiresAt.ToString("o"),
            clientState = "StudioCRM-Outlook-Webhook"
        };

        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            "https://graph.microsoft.com/v1.0/subscriptions");

        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", integration.AccessToken);

        request.Content = new StringContent(
            JsonSerializer.Serialize(payload),
            Encoding.UTF8,
            "application/json");

        var response = await _httpClient.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException($"Microsoft subscription error: {body}");

        using var doc = JsonDocument.Parse(body);
        var root = doc.RootElement;

        var subscriptionId = root.GetProperty("id").GetString() ?? string.Empty;

        var existing = await _context.CalendarSubscriptions
            .FirstOrDefaultAsync(x =>
                x.CalendarIntegrationId == integration.Id &&
                x.Provider == "Outlook");

        if (existing is null)
        {
            existing = new CalendarSubscription
            {
                CalendarIntegrationId = integration.Id,
                Provider = "Outlook",
                CreatedAt = DateTime.UtcNow
            };

            await _context.CalendarSubscriptions.AddAsync(existing);
        }

        existing.SubscriptionId = subscriptionId;
        existing.Resource = "me/events";
        existing.ExpiresAt = expiresAt;
        existing.IsActive = true;

        await _context.SaveChangesAsync();
    }

    private async Task EnsureAccessTokenForIntegrationAsync(CalendarIntegration integration)
    {
        if (integration.AccessTokenExpiresAt > DateTime.UtcNow.AddMinutes(2))
            return;

        // Na teraz najprościej użyj istniejącego sync service przez pusty mechanizm nie zrobimy.
        // Jeżeli token wygaśnie, najpierw odpal ręczny sync sesji albo dołożymy publiczny TokenService.
        throw new InvalidOperationException("Access token expired. Reconnect Outlook or add shared token refresh service.");
    }
}