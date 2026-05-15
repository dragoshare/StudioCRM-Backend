using System.Text.Json;
using Microsoft.Extensions.Options;
using StudioCRM.Application.Interfaces.Calendar;
using StudioCRM.Application.Settings;
using StudioCRM.Domain.Entities;
using StudioCRM.Infrastructure.Persistence;

namespace StudioCRM.Infrastructure.Services.Calendar;

public class OutlookTokenService : IOutlookTokenService
{
    private readonly StudioCRMDbContext _context;
    private readonly OutlookSettings _settings;
    private readonly HttpClient _httpClient;

    public OutlookTokenService(
        StudioCRMDbContext context,
        IOptions<OutlookSettings> options,
        HttpClient httpClient)
    {
        _context = context;
        _settings = options.Value;
        _httpClient = httpClient;
    }

    public async Task EnsureValidAccessTokenAsync(CalendarIntegration integration, bool forceRefresh = false)
    {
        if (!forceRefresh &&
            integration.AccessTokenExpiresAt > DateTime.UtcNow.AddMinutes(5) &&
            !string.IsNullOrWhiteSpace(integration.AccessToken))
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(integration.RefreshToken))
            throw new InvalidOperationException("Outlook refresh token is missing.");

        var tokenUrl = $"https://login.microsoftonline.com/{_settings.TenantId}/oauth2/v2.0/token";

        var form = new Dictionary<string, string>
        {
            ["client_id"] = _settings.ClientId,
            ["client_secret"] = _settings.ClientSecret,
            ["refresh_token"] = integration.RefreshToken,
            ["grant_type"] = "refresh_token",
            ["scope"] = _settings.Scopes
        };

        var response = await _httpClient.PostAsync(tokenUrl, new FormUrlEncodedContent(form));
        var body = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException($"Microsoft refresh token error: {body}");

        using var doc = JsonDocument.Parse(body);
        var root = doc.RootElement;

        integration.AccessToken = root.GetProperty("access_token").GetString() ?? string.Empty;

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
