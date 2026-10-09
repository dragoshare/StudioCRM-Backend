using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using StudioCRM.Application.Settings;
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
    private readonly OutlookSettings _settings;

    public OutlookContactService(
        StudioCRMDbContext context,
        ICurrentUserService currentUser,
        IOutlookTokenService tokenService,
        HttpClient httpClient,
        IOptions<OutlookSettings> settings)
    {
        _context = context;
        _currentUser = currentUser;
        _tokenService = tokenService;
        _httpClient = httpClient;
        _settings = settings.Value;
    }

    public async Task<int> PrepareCalendarAddressesAsync()
    {
        if (!_currentUser.IsOwner) throw new UnauthorizedAccessException("Only owner can prepare calendar addresses.");
        var domain = ClientCalendarAddress.ValidateDomain(_settings.CalendarEmailDomain);
        var clients = await _context.Clients.IgnoreQueryFilters()
            .Where(c => c.CalendarEmail == null || c.CalendarEmail == "").ToListAsync();
        foreach (var client in clients) ClientCalendarAddress.Ensure(client, domain);
        await _context.SaveChangesAsync();
        return clients.Count;
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
                (_settings.UseCalendarEmails || !string.IsNullOrWhiteSpace(c.Email)))
            .ToListAsync();

        if (_settings.UseCalendarEmails)
        {
            foreach (var client in clients)
                ClientCalendarAddress.Ensure(client, _settings.CalendarEmailDomain);
            await _context.SaveChangesAsync();
        }

        foreach (var client in clients)
        {
            await UpsertOutlookContactAsync(integration.AccessToken, client);
        }
    }

    private async Task UpsertOutlookContactAsync(string accessToken, Client client)
    {
        var email = ClientCalendarAddress.Recipient(client, _settings);

        var existingContactId = await FindContactIdByEmailAsync(accessToken, email);
        if (existingContactId == null && _settings.UseCalendarEmails && !string.IsNullOrWhiteSpace(client.Email))
        {
            var sharedEmail = await _context.Clients.AnyAsync(c => c.Id != client.Id && c.Email.ToLower() == client.Email.ToLower());
            if (!sharedEmail)
                existingContactId = await FindContactIdByEmailAsync(accessToken, client.Email.Trim().ToLowerInvariant());
        }

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
            "&$filter=" + Uri.EscapeDataString($"emailAddresses/any(a:a/address eq '{email.Replace("'", "''")}')");

        using var request = new HttpRequestMessage(HttpMethod.Get, url);

        request.Headers.Authorization =
            new AuthenticationHeaderValue("Bearer", accessToken);

        var response = await _httpClient.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException($"Microsoft contact lookup failed ({(int)response.StatusCode}). No contact was created.");

        using var doc = JsonDocument.Parse(body);

        if (!doc.RootElement.TryGetProperty("value", out var value))
            throw new InvalidOperationException("Microsoft contact lookup returned an invalid response. No contact was created.");

        if (value.GetArrayLength() > 1)
            throw new InvalidOperationException("Multiple Outlook contacts have this email. Resolve duplicates before synchronizing.");
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
