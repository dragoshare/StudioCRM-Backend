using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using StudioCRM.Application.Settings;
using StudioCRM.Domain.Entities;
using StudioCRM.Infrastructure.Services.Calendar;

namespace StudioCRM.Tests.IntegrationTests;

// Incident 21.10.2026: a test backend on a copy of the production database used the copied Outlook tokens and
// subscriptions. The database is bound to one backend; any other one makes no Microsoft Graph call at all.
public class OutlookInstanceBindingTests
{
    private const string Production = "https://studiocrm-backend.onrender.com/api/outlook/webhook";

    [Fact]
    public void FingerprintIdentifiesDatabaseAndBackend()
    {
        const string prod = "Host=aws-0-eu.pooler.supabase.com;Port=6543;Database=postgres;Username=postgres.prodref;Password=a";
        const string copy = "Host=aws-0-eu.pooler.supabase.com;Port=6543;Database=postgres;Username=postgres.testref;Password=a";
        var bound = OutlookInstanceBinding.Fingerprint(prod, Production);

        // Same database and backend: password, letter case and a trailing slash do not matter.
        Assert.Equal(bound, OutlookInstanceBinding.Fingerprint(prod.Replace("Password=a", "Password=b").ToUpperInvariant(), Production + "/"));
        // A copy in another Supabase project, even with the production webhook URL copied into its settings.
        Assert.NotEqual(bound, OutlookInstanceBinding.Fingerprint(copy, Production));
        // Another backend on the same database.
        Assert.NotEqual(bound, OutlookInstanceBinding.Fingerprint(prod, "https://studiocrm-test.onrender.com/api/outlook/webhook"));
    }

    [PostgresFact]
    public async Task FirstBackendClaimsTheDatabaseAndAnotherOneCallsNoGraph()
    {
        await using var database = await TestDatabase.CreateAsync();
        await using var db = database.Context();
        var user = new User { Email = "binding-trainer@test.invalid" };
        var integration = new CalendarIntegration
        {
            User = user, Provider = "Outlook", IsActive = true, Email = user.Email,
            AccessToken = "valid", RefreshToken = "refresh", AccessTokenExpiresAt = DateTime.UtcNow.AddHours(1)
        };
        db.Add(integration);
        await db.SaveChangesAsync();

        var handler = new CountingHandler();
        OutlookTokenService Tokens(string webhookUrl) =>
            new(db, Options.Create(new OutlookSettings { WebhookUrl = webhookUrl, TenantId = "common" }), new HttpClient(handler));

        // Production claims the database; a valid token needs no refresh.
        await Tokens(Production).EnsureValidAccessTokenAsync(integration);
        Assert.True(await OutlookInstanceBinding.IsAllowedAsync(db, Production));
        Assert.Equal(OutlookInstanceBinding.Fingerprint(db.Database.GetConnectionString(), Production),
            await db.SystemSettings.Where(s => s.Key == OutlookInstanceBinding.SettingKey).Select(s => s.Value).SingleAsync());

        // Another backend: refused even with a still valid access token and even for a forced refresh.
        const string other = "https://studiocrm-test.onrender.com/api/outlook/webhook";
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => Tokens(other).EnsureValidAccessTokenAsync(integration, forceRefresh: true));
        Assert.Equal(OutlookInstanceBinding.BlockedMessage, error.Message);
        Assert.False(await OutlookInstanceBinding.IsAllowedAsync(db, other));
        Assert.Equal(0, handler.Calls);

        // Deliberate move: removing the binding lets the next backend claim it.
        await db.SystemSettings.Where(s => s.Key == OutlookInstanceBinding.SettingKey).ExecuteDeleteAsync();
        OutlookInstanceBinding.ClearCache();
        Assert.True(await OutlookInstanceBinding.IsAllowedAsync(db, other));
        Assert.False(await OutlookInstanceBinding.IsAllowedAsync(db, Production));
    }

    private sealed class CountingHandler : HttpMessageHandler
    {
        public int Calls;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref Calls);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.BadRequest));
        }
    }
}
