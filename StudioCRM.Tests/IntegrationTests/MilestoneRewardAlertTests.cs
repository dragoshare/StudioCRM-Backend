using Microsoft.EntityFrameworkCore;
using StudioCRM.Application.DTOs.Alerts;
using StudioCRM.Domain.Entities;
using StudioCRM.Infrastructure.Services;

namespace StudioCRM.Tests.IntegrationTests;

public class MilestoneRewardAlertTests
{
    private static OperationalAlertFilterDto Filter => new() { Type = "MilestoneRewardPending", Limit = 200 };

    [PostgresFact]
    public async Task OnlyAssignedTrainerGetsRecentUnclaimedRewardsOfNotInactiveClients()
    {
        await using var database = await TestDatabase.CreateAsync();
        await using var db = database.Context();
        var today = DateTime.UtcNow.Date;
        var location = new Location { Name = "Studio" };
        var trainer = new Trainer { User = new User { Email = "reward-alert@test.invalid" } };
        var other = new Trainer { User = new User { Email = "reward-alert-other@test.invalid" } };
        var owner = new User { Email = "reward-alert-owner@test.invalid" };
        Client Member(string name, Trainer? coach, DateTime start, string status = "Active") => new()
        { FirstName = name, LastName = "Klient", Trainer = coach, Location = location, TrainingStartDate = start, Status = status };
        // Reached the 1-month milestone 5 days ago; 3-month one not yet.
        var recent = Member("Recent", trainer, today.AddMonths(-1).AddDays(-5));
        // Reached the 1-month milestone long ago (old client), outside the alert window.
        var old = Member("Old", trainer, today.AddMonths(-1).AddDays(-90));
        var inactive = Member("Inactive", trainer, today.AddMonths(-1).AddDays(-5), "Inactive");
        var claimed = Member("Claimed", trainer, today.AddMonths(-1).AddDays(-5));
        var foreign = Member("Foreign", other, today.AddMonths(-1).AddDays(-5));
        var unassigned = Member("Unassigned", null, today.AddMonths(-1).AddDays(-5));
        // 1-month milestone reached ~2 months ago (outside), 3-month one 2 days ago (inside): one alert, for the 3-month one.
        var both = Member("Both", trainer, today.AddMonths(-3).AddDays(-2));
        var month = new MilestoneDefinition { Name = "Miesiąc", RequiredMonths = 1, RewardName = "Shaker" };
        var quarter = new MilestoneDefinition { Name = "Kwartał", RequiredMonths = 3, RewardName = "Koszulka" };
        db.AddRange(recent, old, inactive, foreign, unassigned, both, quarter, owner,
            new ClientMilestone { Client = claimed, MilestoneDefinition = month, AchievedAt = today, IsRewardClaimed = true });
        await db.SaveChangesAsync();

        var alerts = (await new OperationalAlertService(db, new PortalSecurityTests.Actor(trainer.UserId)).GetAlertsAsync(Filter)).Items;
        Assert.Equal(
            new[] { (recent.Id, month.Id), (both.Id, quarter.Id) }.Order(),
            alerts.Select(a => (a.ClientId!.Value, a.MilestoneDefinitionId!.Value)).Order());
        var alert = alerts.Single(a => a.ClientId == recent.Id);
        Assert.Equal("Recent Klient: Miesiąc — Shaker", alert.Message);
        Assert.Equal(recent.TrainingStartDate!.Value.AddMonths(1), alert.CreatedAt);
        Assert.Equal($"/clients/{recent.Id}/workspace", alert.ActionUrl);

        Assert.Equal([foreign.Id], (await new OperationalAlertService(db, new PortalSecurityTests.Actor(other.UserId)).GetAlertsAsync(Filter))
            .Items.Select(a => a.ClientId!.Value));
        Assert.Empty((await new OperationalAlertService(db, new PortalSecurityTests.Actor(owner.Id, "Owner")).GetAlertsAsync(Filter)).Items);
    }

    [PostgresFact]
    public async Task EachClientMilestoneGetsItsOwnNotificationResolvedByClaim()
    {
        await using var database = await TestDatabase.CreateAsync();
        await using var db = database.Context();
        var today = DateTime.UtcNow.Date;
        var trainer = new Trainer { User = new User { Email = "reward-notify@test.invalid" } };
        var client = new Client { FirstName = "Anna", LastName = "Nowak", Trainer = trainer, Location = new Location { Name = "S" },
            TrainingStartDate = today.AddMonths(-3).AddDays(-2), Status = "Active" };
        var month = new MilestoneDefinition { Name = "1m", RequiredMonths = 1, RewardName = "Shaker" };
        var quarter = new MilestoneDefinition { Name = "3m", RequiredMonths = 3, RewardName = "Koszulka" };
        // Only the 3-month milestone falls into the 30-day window; the 1-month one was reached ~2 months ago.
        db.AddRange(client, month, quarter);
        await db.SaveChangesAsync();
        var actor = new PortalSecurityTests.Actor(trainer.UserId);
        var notifications = new NotificationService(db, actor, new OperationalAlertService(db, actor));

        var unread = await notifications.GetCurrentUserNotificationsAsync(50, "milestones", false);
        var single = Assert.Single(unread);
        Assert.Equal("MilestoneRewardPending", single.Type);
        Assert.Contains("Koszulka", single.Message);

        db.Add(new ClientMilestone { ClientId = client.Id, MilestoneDefinitionId = quarter.Id, AchievedAt = today, IsRewardClaimed = true });
        await db.SaveChangesAsync();
        Assert.Empty(await notifications.GetCurrentUserNotificationsAsync(50, "milestones", false));
        Assert.True(await db.Notifications.AnyAsync(n => n.Type == "MilestoneRewardPending" && n.IsRead &&
            n.SourceKey == $"operational:MilestoneRewardPending:client:{client.Id}:milestone:{quarter.Id}"));
    }
}
