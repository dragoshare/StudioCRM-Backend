using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
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
    private readonly ILogger<OutlookSubscriptionService> _logger;

    public OutlookSubscriptionService(
        StudioCRMDbContext context,
        ICurrentUserService currentUser,
        IOutlookTokenService tokenService,
        HttpClient httpClient,
        IConfiguration configuration,
        ILogger<OutlookSubscriptionService> logger)
    {
        _context = context;
        _currentUser = currentUser;
        _tokenService = tokenService;
        _httpClient = httpClient;
        _configuration = configuration;
        _logger = logger;
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

        await CreateSubscriptionForIntegrationAsync(integration);
    }

    private async Task CreateSubscriptionForIntegrationAsync(CalendarIntegration integration)
    {
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

        var response = await SendCreateSubscriptionRequestAsync(integration.AccessToken, payload);
        var body = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode && IsInvalidAuthenticationToken(response, body))
        {
            await _tokenService.EnsureValidAccessTokenAsync(integration, forceRefresh: true);

            response = await SendCreateSubscriptionRequestAsync(integration.AccessToken, payload);
            body = await response.Content.ReadAsStringAsync();
        }

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

    private async Task<HttpResponseMessage> SendCreateSubscriptionRequestAsync(string accessToken, object payload)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            "https://graph.microsoft.com/v1.0/subscriptions");

        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        request.Content = new StringContent(
            JsonSerializer.Serialize(payload),
            Encoding.UTF8,
            "application/json");

        return await _httpClient.SendAsync(request);
    }

    private static bool IsInvalidAuthenticationToken(HttpResponseMessage response, string body)
    {
        return response.StatusCode == System.Net.HttpStatusCode.Unauthorized &&
               body.Contains("InvalidAuthenticationToken", StringComparison.OrdinalIgnoreCase);
    }

    public async Task RenewExpiringSubscriptionsAsync()
    {
        var integrations = await _context.CalendarIntegrations
            .Where(x =>
                x.Provider == "Outlook" &&
                x.IsActive)
            .ToListAsync();

        foreach (var integration in integrations)
        {
            try
            {
                await EnsureSubscriptionForIntegrationAsync(integration);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    ex,
                    "Outlook subscription check failed for integration {IntegrationId}.",
                    integration.Id);
            }
        }
    }

    private async Task EnsureSubscriptionForIntegrationAsync(CalendarIntegration integration)
    {
        var subscription = await _context.CalendarSubscriptions
            .Include(x => x.CalendarIntegration)
            .FirstOrDefaultAsync(x =>
                x.CalendarIntegrationId == integration.Id &&
                x.Provider == "Outlook");

        var now = DateTime.UtcNow;

        if (subscription is null || !subscription.IsActive || subscription.ExpiresAt <= now)
        {
            await CreateSubscriptionForIntegrationAsync(integration);
            return;
        }

        if (subscription.ExpiresAt > now.AddHours(24))
            return;

        try
        {
            await RenewSubscriptionAsync(subscription);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Outlook subscription renewal failed for integration {IntegrationId}; recreating subscription.",
                integration.Id);

            await CreateSubscriptionForIntegrationAsync(integration);
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
