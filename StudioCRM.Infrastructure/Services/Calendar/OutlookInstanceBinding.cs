using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using StudioCRM.Infrastructure.Persistence;

namespace StudioCRM.Infrastructure.Services.Calendar;

// A copy of the production database (e.g. restored for a test backend) carries the trainers' Outlook tokens and
// subscription ids. A second backend using them deletes/recreates the production subscriptions and edits the same
// events, and every event edit e-mails the attendees (incident 21.10.2026: a mailbox blocked for mass mailing).
// The database therefore remembers the one backend allowed to call Microsoft: its database identity (host, port,
// database, user) plus its Outlook:WebhookUrl. The first backend to use Outlook claims it; any other backend gets no
// Microsoft Graph access at all (no token refresh, subscriptions, sync or webhook handling).
// Moving Outlook on purpose (new database host, new backend URL):
//   DELETE FROM "SystemSettings" WHERE "Key" = 'Outlook:InstanceBinding';   -- the next Outlook call claims it again
internal static class OutlookInstanceBinding
{
    public const string SettingKey = "Outlook:InstanceBinding";
    public const string BlockedMessage =
        "Outlook jest powiązany z innym backendem lub inną bazą (np. to kopia bazy produkcyjnej). " +
        "Ten backend nie wykonuje żadnych operacji w Outlooku.";

    private static readonly TimeSpan CacheFor = TimeSpan.FromMinutes(5);
    private static readonly ConcurrentDictionary<string, (bool Allowed, DateTime CheckedAt)> Cache = new();

    public static string Fingerprint(string? connectionString, string? webhookUrl)
    {
        var builder = new NpgsqlConnectionStringBuilder(connectionString ?? string.Empty);
        var identity = string.Join("|",
            Normalize(builder.Host), builder.Port, Normalize(builder.Database), Normalize(builder.Username),
            Normalize(webhookUrl).TrimEnd('/'));
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(identity)))[..32].ToLowerInvariant();
    }

    public static async Task<bool> IsAllowedAsync(StudioCRMDbContext context, string? webhookUrl)
    {
        var connectionString = context.Database.GetConnectionString();
        var fingerprint = Fingerprint(connectionString, webhookUrl);
        // Keyed by the full connection (incl. schema), so separate databases/schemas are never mixed up.
        var cacheKey = $"{connectionString}|{webhookUrl}";
        if (Cache.TryGetValue(cacheKey, out var cached) && DateTime.UtcNow - cached.CheckedAt < CacheFor)
            return cached.Allowed;

        // Raw SQL, so the claim never flushes other tracked changes of the caller's context.
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"""INSERT INTO "SystemSettings" ("Key", "Value", "CreatedAt", "UpdatedAt") VALUES ({SettingKey}, {fingerprint}, now(), now()) ON CONFLICT ("Key") DO NOTHING""");
        var bound = await context.SystemSettings.AsNoTracking()
            .Where(s => s.Key == SettingKey)
            .Select(s => s.Value)
            .FirstOrDefaultAsync();

        var allowed = string.Equals(bound, fingerprint, StringComparison.Ordinal);
        Cache[cacheKey] = (allowed, DateTime.UtcNow);
        return allowed;
    }

    public static async Task EnsureAllowedAsync(StudioCRMDbContext context, string? webhookUrl)
    {
        if (!await IsAllowedAsync(context, webhookUrl))
            throw new InvalidOperationException(BlockedMessage);
    }

    internal static void ClearCache() => Cache.Clear();

    private static string Normalize(string? value) => (value ?? string.Empty).Trim().ToLowerInvariant();
}
