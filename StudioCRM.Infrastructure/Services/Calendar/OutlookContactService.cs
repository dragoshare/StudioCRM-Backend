using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
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
    private readonly ILogger<OutlookContactService>? _logger;

    public OutlookContactService(
        StudioCRMDbContext context,
        ICurrentUserService currentUser,
        IOutlookTokenService tokenService,
        HttpClient httpClient,
        IOptions<OutlookSettings> settings,
        ILogger<OutlookContactService>? logger = null)
    {
        _context = context;
        _currentUser = currentUser;
        _tokenService = tokenService;
        _httpClient = httpClient;
        _settings = settings.Value;
        _logger = logger;
    }

    public async Task<int> PrepareCalendarAddressesAsync()
    {
        if (!_currentUser.IsOwner) throw new UnauthorizedAccessException("Only owner can prepare calendar addresses.");
        var domain = ClientCalendarAddress.ValidateDomain(_settings.CalendarEmailDomain);
        var clients = await _context.Clients.IncludingOrganizationArchive(_context.OrganizationId)
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

        // Owners already see every client in CRM, so their Outlook gets the whole client list with full details.
        if (_currentUser.IsOwner)
        {
            var allClients = await _context.Clients
                .Where(c => !c.IsDeleted && (_settings.UseCalendarEmails || !string.IsNullOrWhiteSpace(c.Email)))
                .OrderBy(c => c.Id)
                .ToListAsync();
            if (_settings.UseCalendarEmails)
            {
                foreach (var client in allClients)
                    ClientCalendarAddress.Ensure(client, _settings.CalendarEmailDomain);
                await _context.SaveChangesAsync();
            }
            foreach (var client in allClients)
                await UpsertOutlookContactAsync(integration.AccessToken, client);
            return;
        }

        if (trainer == null)
            throw new InvalidOperationException("Current user is not a trainer.");

        // Own clients plus every client of the trainer's locations, so a substitute can invite them to an event.
        // Other trainers' clients are shared only through technical calendar addresses and without phone numbers.
        var locationIds = await _context.TrainerLocations
            .Where(l => l.TrainerId == trainer.Id)
            .Select(l => l.LocationId)
            .ToListAsync();

        var clients = await _context.Clients
            .Where(c =>
                !c.IsDeleted &&
                (c.TrainerId == trainer.Id
                    ? _settings.UseCalendarEmails || !string.IsNullOrWhiteSpace(c.Email)
                    : _settings.UseCalendarEmails &&
                        (locationIds.Contains(c.LocationId) ||
                         c.LocationMemberships.Any(m => locationIds.Contains(m.LocationId)))))
            .OrderBy(c => c.Id)
            .ToListAsync();

        if (_settings.UseCalendarEmails)
        {
            foreach (var client in clients)
                ClientCalendarAddress.Ensure(client, _settings.CalendarEmailDomain);
            await _context.SaveChangesAsync();
        }

        foreach (var client in clients)
        {
            var contact = client.TrainerId == trainer.Id ? client : SharedContact(client);
            await UpsertOutlookContactAsync(integration.AccessToken, contact);
        }
    }

    public async Task SyncClientToConnectedOutlooksAsync(int clientId)
    {
        var client = await _context.Clients
            .Include(c => c.LocationMemberships)
            .FirstOrDefaultAsync(c => c.Id == clientId && !c.IsDeleted);
        if (client == null)
            return;

        var integrations = await _context.CalendarIntegrations
            .Where(x => x.Provider == "Outlook" && x.IsActive && x.User.IsActive)
            .OrderBy(x => x.Id)
            .ToListAsync();
        if (integrations.Count == 0)
            return;

        var clientLocationIds = client.LocationMemberships.Select(m => m.LocationId).Append(client.LocationId).Distinct().ToList();
        var userIds = integrations.Select(x => x.UserId).Distinct().ToList();
        var ownerIds = await OwnerUserIdsAsync(userIds);
        var trainers = await _context.Trainers
            .Where(t => userIds.Contains(t.UserId) && !t.IsDeleted)
            .Select(t => new { t.Id, t.UserId, AtClientLocation = t.TrainerLocations.Any(l => clientLocationIds.Contains(l.LocationId)) })
            .ToListAsync();
        var hasContactEmail = _settings.UseCalendarEmails || !string.IsNullOrWhiteSpace(client.Email);

        foreach (var integration in integrations)
        {
            var trainer = trainers.FirstOrDefault(t => t.UserId == integration.UserId);
            // Same rules as SyncClientsAsync: owners and the client's own trainer get full details; other trainers
            // of the client's locations only the technical calendar address, without the phone number.
            Client? contact =
                ownerIds.Contains(integration.UserId) || trainer?.Id == client.TrainerId ? (hasContactEmail ? client : null)
                : trainer is { AtClientLocation: true } && _settings.UseCalendarEmails ? SharedContact(client)
                : null;
            if (contact == null)
                continue;

            try
            {
                await _tokenService.EnsureValidAccessTokenAsync(integration);
                await UpsertOutlookContactAsync(integration.AccessToken, contact);
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Could not push client {ClientId} to Outlook contacts of integration {IntegrationId}.",
                    client.Id, integration.Id);
            }
        }
    }

    private async Task<HashSet<int>> OwnerUserIdsAsync(List<int> userIds)
    {
        // Legacy organization keeps account roles; other organizations grant Owner per membership (see AuthService.Organizations).
        var owners = _context.OrganizationId == StudioCRMDbContext.LegacyOrganizationId
            ? await _context.Users.Where(u => userIds.Contains(u.Id) && u.UserRoles.Any(r => r.Role.Name == "Owner"))
                .Select(u => u.Id).ToListAsync()
            : await _context.Set<OrganizationMembership>()
                .Where(m => m.OrganizationId == _context.OrganizationId && m.IsOwner && userIds.Contains(m.UserId))
                .Select(m => m.UserId).ToListAsync();
        return owners.ToHashSet();
    }

    private static Client SharedContact(Client client) => new()
    {
        Id = client.Id, FirstName = client.FirstName, LastName = client.LastName,
        Email = client.Email, CalendarEmail = client.CalendarEmail
    };

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
