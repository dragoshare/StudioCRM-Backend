using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using StudioCRM.Application.DTOs.Calendar;
using StudioCRM.Application.Interfaces;
using StudioCRM.Application.Interfaces.Calendar;
using StudioCRM.Application.Settings;
using StudioCRM.Domain.Entities;
using StudioCRM.Infrastructure.Persistence;

namespace StudioCRM.Infrastructure.Services.Calendar;

public class OutlookCalendarAuthService : IOutlookCalendarAuthService
{
    private readonly StudioCRMDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly OutlookSettings _settings;
    private readonly HttpClient _httpClient;

    public OutlookCalendarAuthService(
        StudioCRMDbContext context,
        ICurrentUserService currentUser,
        IOptions<OutlookSettings> options,
        HttpClient httpClient)
    {
        _context = context;
        _currentUser = currentUser;
        _settings = options.Value;
        _httpClient = httpClient;
    }

    public Task<CalendarConnectUrlDto> GetConnectUrlAsync()
    {
        if (!_currentUser.UserId.HasValue)
            throw new InvalidOperationException("User is not authenticated.");

        var state = _currentUser.UserId.Value.ToString();

        var url =
            $"https://login.microsoftonline.com/{_settings.TenantId}/oauth2/v2.0/authorize" +
            $"?client_id={Uri.EscapeDataString(_settings.ClientId)}" +
            $"&response_type=code" +
            $"&redirect_uri={Uri.EscapeDataString(_settings.RedirectUri)}" +
            $"&response_mode=query" +
            $"&scope={Uri.EscapeDataString(_settings.Scopes)}" +
            $"&prompt=consent" +
            $"&state={Uri.EscapeDataString(state)}";

        return Task.FromResult(new CalendarConnectUrlDto { Url = url });
    }

    public async Task ConnectCallbackAsync(string code, string? state)
    {
        if (string.IsNullOrWhiteSpace(state) || !int.TryParse(state, out var userId))
            throw new InvalidOperationException("Invalid state.");

        var token = await ExchangeCodeForTokenAsync(code);

        if (string.IsNullOrWhiteSpace(token.AccessToken))
            throw new InvalidOperationException("Access token is empty!");

        var profile = await GetMicrosoftProfileAsync(token.AccessToken);

        var integration = await _context.CalendarIntegrations
            .FirstOrDefaultAsync(x => x.UserId == userId && x.Provider == "Outlook");

        if (integration is null)
        {
            integration = new CalendarIntegration
            {
                UserId = userId,
                Provider = "Outlook",
                ConnectedAt = DateTime.UtcNow
            };

            await _context.CalendarIntegrations.AddAsync(integration);
        }

        integration.ExternalUserId = profile.Id;
        integration.Email = profile.Email;
        integration.AccessToken = token.AccessToken;
        integration.RefreshToken = token.RefreshToken;
        integration.AccessTokenExpiresAt = DateTime.UtcNow.AddSeconds(token.ExpiresIn - 60);
        integration.IsActive = true;
        integration.DisconnectedAt = null;

        await _context.SaveChangesAsync();
    }

    public async Task<CalendarIntegrationStatusDto> GetStatusAsync()
    {
        if (!_currentUser.UserId.HasValue)
            return new CalendarIntegrationStatusDto { IsConnected = false };

        var integration = await _context.CalendarIntegrations
            .FirstOrDefaultAsync(x =>
                x.UserId == _currentUser.UserId.Value &&
                x.Provider == "Outlook" &&
                x.IsActive);

        if (integration is null)
            return new CalendarIntegrationStatusDto { IsConnected = false };

        return new CalendarIntegrationStatusDto
        {
            IsConnected = true,
            Provider = integration.Provider,
            Email = integration.Email,
            ConnectedAt = integration.ConnectedAt
        };
    }

    public async Task DisconnectAsync()
    {
        if (!_currentUser.UserId.HasValue)
            throw new InvalidOperationException("User is not authenticated.");

        var integration = await _context.CalendarIntegrations
            .FirstOrDefaultAsync(x =>
                x.UserId == _currentUser.UserId.Value &&
                x.Provider == "Outlook");

        if (integration is null)
            return;

        integration.IsActive = false;
        integration.DisconnectedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();
    }

    // 🔥 KLUCZOWA METODA (NAPRAWIONA)
    private async Task<OutlookTokenResponse> ExchangeCodeForTokenAsync(string code)
    {
        var tokenUrl = $"https://login.microsoftonline.com/{_settings.TenantId}/oauth2/v2.0/token";

        var form = new Dictionary<string, string>
        {
            ["client_id"] = _settings.ClientId,
            ["client_secret"] = _settings.ClientSecret,
            ["code"] = code,
            ["redirect_uri"] = _settings.RedirectUri,
            ["grant_type"] = "authorization_code",
            ["scope"] = _settings.Scopes
        };

        var response = await _httpClient.PostAsync(tokenUrl, new FormUrlEncodedContent(form));
        var body = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException($"Microsoft token error: {body}");

        using var doc = JsonDocument.Parse(body);
        var root = doc.RootElement;

        return new OutlookTokenResponse
        {
            TokenType = root.GetProperty("token_type").GetString() ?? "",
            Scope = root.GetProperty("scope").GetString() ?? "",
            ExpiresIn = root.GetProperty("expires_in").GetInt32(),
            AccessToken = root.GetProperty("access_token").GetString() ?? "",
            RefreshToken = root.TryGetProperty("refresh_token", out var r)
                ? r.GetString() ?? ""
                : ""
        };
    }

    private async Task<OutlookProfile> GetMicrosoftProfileAsync(string accessToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "https://graph.microsoft.com/v1.0/me");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        var response = await _httpClient.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException($"Microsoft profile error: {body}");

        using var doc = JsonDocument.Parse(body);
        var root = doc.RootElement;

        return new OutlookProfile
        {
            Id = root.GetProperty("id").GetString() ?? "",
            Email = root.TryGetProperty("mail", out var mail)
                ? mail.GetString() ?? ""
                : root.GetProperty("userPrincipalName").GetString() ?? ""
        };
    }

    private class OutlookTokenResponse
    {
        public string TokenType { get; set; } = "";
        public string Scope { get; set; } = "";
        public int ExpiresIn { get; set; }
        public string AccessToken { get; set; } = "";
        public string RefreshToken { get; set; } = "";
    }

    private class OutlookProfile
    {
        public string Id { get; set; } = "";
        public string Email { get; set; } = "";
    }
}
