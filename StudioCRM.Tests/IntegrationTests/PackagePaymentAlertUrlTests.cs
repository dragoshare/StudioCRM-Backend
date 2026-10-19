using StudioCRM.Application.DTOs.Alerts;
using StudioCRM.Domain.Entities;
using StudioCRM.Infrastructure.Services;

namespace StudioCRM.Tests.IntegrationTests;

public class PackagePaymentAlertUrlTests
{
    // "Pakiet wymaga opłacenia" opens the payments of the client it concerns, in the owner or trainer panel.
    [PostgresFact]
    public async Task PackagePaymentRequiredLinksToClientPayments()
    {
        await using var database = await TestDatabase.CreateAsync();
        await using var db = database.Context();
        var location = new Location { Name = "Alert studio" };
        var trainer = new Trainer { User = new User { Email = "alert-trainer@test.invalid" } };
        var client = new Client { FirstName = "Due", LastName = "Client", Trainer = trainer, Location = location };
        db.Add(new ClientPackage { Client = client, Package = new Package { Name = "Personal" }, Name = "Personal",
            TotalSessions = 4, TotalPrice = 400, PurchaseDate = DateTime.UtcNow });
        await db.SaveChangesAsync();

        async Task<string?> UrlFor(PortalSecurityTests.Actor actor) =>
            (await new OperationalAlertService(db, actor).GetAlertsAsync(new OperationalAlertFilterDto { Type = "PackagePaymentRequired" }))
                .Items.Single(a => a.ClientId == client.Id).ActionUrl;

        Assert.Equal($"/owner/clients/{client.Id}/payments", await UrlFor(new PortalSecurityTests.Actor(int.MaxValue, "Owner")));
        Assert.Equal($"/trainer/clients/{client.Id}/payments", await UrlFor(new PortalSecurityTests.Actor(trainer.UserId)));
    }
}
