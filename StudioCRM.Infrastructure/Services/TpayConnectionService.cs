using System.Text.Json;
using Microsoft.Extensions.Options;
using StudioCRM.Application.Interfaces;
using StudioCRM.Application.Settings;

namespace StudioCRM.Infrastructure.Services;

public class TpayConnectionService(HttpClient httpClient, IOptions<TpaySettings> options)
    : ITpayConnectionService
{
    public async Task<bool> TestConnectionAsync(string accountKey, CancellationToken cancellationToken)
    {
        var settings = options.Value;
        if (!settings.Accounts.TryGetValue(accountKey, out var account) ||
            string.IsNullOrWhiteSpace(account.ClientId) || string.IsNullOrWhiteSpace(account.ClientSecret))
            throw new InvalidOperationException("Tpay credentials are not configured for this account key.");

        var endpoint = settings.UseSandbox
            ? "https://openapi.sandbox.tpay.com/oauth/auth"
            : "https://api.tpay.com/oauth/auth";

        using var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["client_id"] = account.ClientId,
            ["client_secret"] = account.ClientSecret
        });
        using var response = await httpClient.PostAsync(endpoint, content, cancellationToken);
        if (!response.IsSuccessStatusCode)
            throw new HttpRequestException("Tpay rejected the connection check.", null, response.StatusCode);

        // The diagnostic must never return or log the provider's credentials or token.
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
        if (!document.RootElement.TryGetProperty("access_token", out var token) ||
            token.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(token.GetString()))
            throw new HttpRequestException("Tpay did not return an access token.");

        return settings.UseSandbox;
    }
}
