using StudioCRM.Application.Settings;
using StudioCRM.Domain.Entities;

namespace StudioCRM.Infrastructure.Services.Calendar;

public static class ClientCalendarAddress
{
    public static bool IsTechnical(string? email, string domain)
    {
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(domain)) return false;
        var parts = email.Trim().Split('@');
        return parts.Length == 2 && parts[0].StartsWith("klient-", StringComparison.OrdinalIgnoreCase) &&
               parts[1].Equals(domain.Trim(), StringComparison.OrdinalIgnoreCase);
    }

    public static string ValidateDomain(string domain)
    {
        var normalized = domain.Trim().ToLowerInvariant();
        if (normalized.Length > 253 || !normalized.Contains('.') ||
            normalized.EndsWith('.') || Uri.CheckHostName(normalized) != UriHostNameType.Dns)
            throw new InvalidOperationException("Outlook:CalendarEmailDomain must be a valid DNS domain.");
        return normalized;
    }

    public static bool Ensure(Client client, string domain)
    {
        if (!string.IsNullOrWhiteSpace(client.CalendarEmail)) return false;
        client.CalendarEmail = $"klient-{Guid.NewGuid():N}@{ValidateDomain(domain)}";
        return true;
    }

    public static string Recipient(Client client, OutlookSettings settings)
    {
        if (!settings.UseCalendarEmails) return client.Email.Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(client.CalendarEmail))
            throw new InvalidOperationException($"Client {client.Id} has no calendar address. Prepare calendar addresses first.");
        return client.CalendarEmail;
    }
}
