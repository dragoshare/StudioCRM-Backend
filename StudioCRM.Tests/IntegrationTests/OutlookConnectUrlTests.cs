using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Options;
using StudioCRM.Application.Settings;
using StudioCRM.Domain.Entities;
using StudioCRM.Infrastructure.Services.Calendar;

namespace StudioCRM.Tests.IntegrationTests;

public class OutlookConnectUrlTests
{
    // Forcing consent (prompt=consent) blocked staff of Microsoft 365 tenants where only an administrator may
    // consent ("Admin approval required"), even after tenant-wide admin consent. The user now picks the account
    // and Microsoft asks for consent only when it is actually missing.
    [PostgresFact]
    public async Task ConnectUrlLetsTheUserPickTheAccountWithoutForcingConsent()
    {
        await using var database = await TestDatabase.CreateAsync();
        await using var db = database.Context();
        var user = new User { Email = "connect-url@test.invalid", IsActive = true };
        db.Add(new UserRole { User = user, Role = new Role { Name = "Trainer" } });
        await db.SaveChangesAsync();
        var service = new OutlookCalendarAuthService(db, new PortalSecurityTests.Actor(user.Id), Options.Create(new OutlookSettings
        { TenantId = "common", ClientId = "test", RedirectUri = "https://api.test.invalid/api/outlook/callback", Scopes = "User.Read" }),
            new HttpClient(), new HttpContextAccessor { HttpContext = new DefaultHttpContext() });

        var query = QueryHelpers.ParseQuery(new Uri((await service.GetConnectUrlAsync()).Url).Query);

        Assert.Equal("select_account", query["prompt"].ToString());
        Assert.Equal("S256", query["code_challenge_method"].ToString());
    }
}
