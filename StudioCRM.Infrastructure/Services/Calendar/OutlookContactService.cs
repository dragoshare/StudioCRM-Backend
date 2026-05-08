using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using StudioCRM.Application.Interfaces;
using StudioCRM.Application.Interfaces.Calendar;
using StudioCRM.Domain.Entities;
using StudioCRM.Infrastructure.Persistence;

namespace StudioCRM.Infrastructure.Services.Calendar;

public class OutlookContactService : IOutlookContactService
{
    private readonly StudioCRMDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly IOutlookTokenService _tokenService;
    private readonly HttpClient _httpClient;

    public OutlookContactService(
        StudioCRMDbContext context,
        ICurrentUserService currentUser,
        IOutlookTokenService tokenService,
        HttpClient httpClient)
    {
        _context = context;
        _currentUser = currentUser;
        _tokenService = tokenService;
        _httpClient = httpClient;
    }

    public async Task SyncClientsAsync()
    {
        if (!_currentUser.UserId.HasValue)
            throw new InvalidOperationException("User is not authenticated.");

        var integration = await _context.CalendarIntegrations
            .FirstOrDefaultAsync(x =>
                x.UserId == _currentUser.UserId.Value &&
                x.Provider == "Outlook" &&
                x.IsActive);

        if (integration == null)
            throw new InvalidOperationException("Outlook is not connected.");

        await _tokenService.EnsureValidAccessTokenAsync(integration);

        var trainer = await _context.Trainers
            .FirstOrDefaultAsync(t => t.UserId == _currentUser.UserId.Value);

        if (trainer == null)
            throw new InvalidOperationException("Current user is not a trainer.");

        var clients = await _context.Clients
            .Where(c =>
                !c.IsDeleted &&
                c.TrainerId == trainer.Id &&
                !string.IsNullOrWhiteSpace(c.Email))
            .ToListAsync();

        foreach (var client in clients)
        {
            await UpsertOutlookContactAsync(integration.AccessToken, client);
        }
    }

    private async Task UpsertOutlookContactAsync(string accessToken, Client client)
    {
        var email = client.Email.Trim().ToLowerInvariant();

        var existingContactId = await FindContactIdByEmailAsync(accessToken, email);

        if (existingContactId == null)
        {
            await CreateContactAsync(accessToken, client, email);
        }
        else
        {
            await UpdateContactAsync(accessToken, existingContactId, client, email);
        }
    }

    private async Task<string?> FindContactIdByEmailAsync(string accessToken, string email)
    {
        var url =
            "https://graph.microsoft.com/v1.0/me/contacts" +
            "?$select=id,emailAddresses" +
            $"&$filter=emailAddresses/any(a:a/address eq '{email}')";

        using var request = new HttpRequestMessage(HttpMethod.Get, url);

        request.Headers.Authorization =
            new AuthenticationHeaderValue("Bearer", accessToken);

        var response = await _httpClient.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
            return null;

        using var doc = JsonDocument.Parse(body);

        if (!doc.RootElement.TryGetProperty("value", out var value))
            return null;

        var first = value.EnumerateArray().FirstOrDefault();

        if (first.ValueKind == JsonValueKind.Undefined)
            return null;

        return first.TryGetProperty("id", out var id)
            ? id.GetString()
            : null;
    }

    private async Task CreateContactAsync(string accessToken, Client client, string email)
    {
        var payload = BuildContactPayload(client, email);

        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            "https://graph.microsoft.com/v1.0/me/contacts");

        request.Headers.Authorization =
            new AuthenticationHeaderValue("Bearer", accessToken);

        request.Content = new StringContent(
            JsonSerializer.Serialize(payload),
            Encoding.UTF8,
            "application/json");

        var response = await _httpClient.SendAsync(request);

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync();
            throw new InvalidOperationException($"Microsoft create contact error: {body}");
        }
    }

    private async Task UpdateContactAsync(string accessToken, string contactId, Client client, string email)
    {
        var payload = BuildContactPayload(client, email);

        using var request = new HttpRequestMessage(
            HttpMethod.Patch,
            $"https://graph.microsoft.com/v1.0/me/contacts/{contactId}");

        request.Headers.Authorization =
            new AuthenticationHeaderValue("Bearer", accessToken);

        request.Content = new StringContent(
            JsonSerializer.Serialize(payload),
            Encoding.UTF8,
            "application/json");

        var response = await _httpClient.SendAsync(request);

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync();
            throw new InvalidOperationException($"Microsoft update contact error: {body}");
        }
    }

    private static object BuildContactPayload(Client client, string email)
    {
        return new
        {
            givenName = client.FirstName,
            surname = client.LastName,
            emailAddresses = new[]
            {
                new
                {
                    address = email,
                    name = $"{client.FirstName} {client.LastName}".Trim()
                }
            },
            businessPhones = string.IsNullOrWhiteSpace(client.PhoneNumber)
                ? Array.Empty<string>()
                : new[] { client.PhoneNumber }
        };
    }
}