using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using StudioCRM.Application.Interfaces;
using StudioCRM.Application.Interfaces.Calendar;
using StudioCRM.Domain.Entities;
using StudioCRM.Infrastructure.Persistence;

namespace StudioCRM.Infrastructure.Services.Calendar;

public class OutlookSubscriptionService : IOutlookSubscriptionService
{
    private readonly StudioCRMDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly IOutlookTokenService _tokenService;
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;

    public OutlookSubscriptionService(
        StudioCRMDbContext context,
        ICurrentUserService currentUser,
        IOutlookTokenService tokenService,
        HttpClient httpClient,
        IConfiguration configuration)
    {
        _context = context;
        _currentUser = currentUser;
        _tokenService = tokenService;
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

        await _tokenService.EnsureValidAccessTokenAsync(integration);

        var webhookUrl = _configuration["Outlook:WebhookUrl"];

        if (string.IsNullOrWhiteSpace(webhookUrl))
            throw new InvalidOperationException("Outlook webhook URL is not configured.");

        await DeleteActiveMicrosoftSubscriptionsForIntegrationAsync(integration);

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

        var expirationDateTime = root.TryGetProperty("expirationDateTime", out var exp)
            ? DateTime.Parse(exp.GetString() ?? expiresAt.ToString("o")).ToUniversalTime()
            : expiresAt;

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
        existing.ExpiresAt = expirationDateTime;
        existing.IsActive = true;

        await _context.SaveChangesAsync();
    }

    public async Task RenewExpiringSubscriptionsAsync()
    {
        var subscriptions = await _context.CalendarSubscriptions
            .Include(x => x.CalendarIntegration)
            .Where(x =>
                x.Provider == "Outlook" &&
                x.IsActive &&
                x.ExpiresAt <= DateTime.UtcNow.AddHours(6))
            .ToListAsync();

        foreach (var subscription in subscriptions)
        {
            try
            {
                await RenewSubscriptionAsync(subscription);
            }
            catch
            {
                // Nie wyłączamy od razu subskrypcji przy chwilowym błędzie Microsoft/Render.
                // Jeśli faktycznie wygasła, create/renew ręczny albo worker spróbuje ponownie.
                subscription.IsActive = subscription.ExpiresAt > DateTime.UtcNow;
                await _context.SaveChangesAsync();
            }
        }
    }

    private async Task RenewSubscriptionAsync(CalendarSubscription subscription)
    {
        var integration = subscription.CalendarIntegration;

        if (!integration.IsActive)
        {
            subscription.IsActive = false;
            await _context.SaveChangesAsync();
            return;
        }

        await _tokenService.EnsureValidAccessTokenAsync(integration);

        var newExpiresAt = DateTime.UtcNow.AddHours(48);

        var payload = new
        {
            expirationDateTime = newExpiresAt.ToString("o")
        };

        using var request = new HttpRequestMessage(
            HttpMethod.Patch,
            $"https://graph.microsoft.com/v1.0/subscriptions/{subscription.SubscriptionId}");

        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", integration.AccessToken);

        request.Content = new StringContent(
            JsonSerializer.Serialize(payload),
            Encoding.UTF8,
            "application/json");

        var response = await _httpClient.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException($"Microsoft renew subscription error: {body}");

        using var doc = JsonDocument.Parse(body);
        var root = doc.RootElement;

        subscription.ExpiresAt = root.TryGetProperty("expirationDateTime", out var exp)
            ? DateTime.Parse(exp.GetString() ?? newExpiresAt.ToString("o")).ToUniversalTime()
            : newExpiresAt;

        subscription.IsActive = true;

        await _context.SaveChangesAsync();
    }

    private async Task DeleteActiveMicrosoftSubscriptionsForIntegrationAsync(CalendarIntegration integration)
    {
        var activeSubscriptions = await _context.CalendarSubscriptions
            .Where(x =>
                x.CalendarIntegrationId == integration.Id &&
                x.Provider == "Outlook" &&
                x.IsActive &&
                !string.IsNullOrWhiteSpace(x.SubscriptionId))
            .ToListAsync();

        foreach (var subscription in activeSubscriptions)
        {
            try
            {
                using var request = new HttpRequestMessage(
                    HttpMethod.Delete,
                    $"https://graph.microsoft.com/v1.0/subscriptions/{subscription.SubscriptionId}");

                request.Headers.Authorization =
                    new AuthenticationHeaderValue("Bearer", integration.AccessToken);

                await _httpClient.SendAsync(request);
            }
            catch
            {
                // Nie blokujemy tworzenia nowej subskrypcji, jeśli stara już nie istnieje po stronie Microsoft.
            }

            subscription.IsActive = false;
        }

        await _context.SaveChangesAsync();
    }
}