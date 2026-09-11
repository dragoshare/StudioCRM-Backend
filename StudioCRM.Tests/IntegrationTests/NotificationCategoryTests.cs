using Microsoft.EntityFrameworkCore;
using StudioCRM.Application.Common;
using StudioCRM.Application.DTOs.Alerts;
using StudioCRM.Application.Interfaces;
using StudioCRM.Domain.Entities;
using StudioCRM.Infrastructure.Services;

namespace StudioCRM.Tests.IntegrationTests;

public class NotificationCategoryTests
{
    [Theory]
    [InlineData("PaymentPendingConfirmation", "payments")]
    [InlineData("PackageEndedPaymentRequired", "payments")]
    [InlineData("PackageEndingSoon", "packages")]
    [InlineData("SessionNotSyncedToOutlook", "schedule")]
    [InlineData("ClientInvitationAccepted", "invitations")]
    [InlineData("TrainerSettlementReminder", "trainers")]
    [InlineData("FutureType", "system")]
    public void TypesHaveStableCategories(string type, string expected) => Assert.Equal(expected, NotificationCategories.Resolve(type));

    [PostgresFact]
    public async Task CategoryFiltersRunBeforeLimitAndRespectUserAndReadScope()
    {
        await using var database = await TestDatabase.CreateAsync();
        await using var db = database.Context();
        var user = new User { Email = "notifications@example.test" };
        var other = new User { Email = "other@example.test" };
        db.Users.AddRange(user, other);
        await db.SaveChangesAsync();
        var payment = new Notification { UserId = user.Id, Type = "PaymentPendingConfirmation", SourceKey = "payment", CreatedAt = DateTime.UtcNow.AddDays(-1) };
        var package = new Notification { UserId = user.Id, Type = "PackageEndingSoon", SourceKey = "package" };
        var unknown = new Notification { UserId = user.Id, Type = "FutureType", SourceKey = "future" };
        var otherPayment = new Notification { UserId = other.Id, Type = "PaymentPendingConfirmation", SourceKey = "payment" };
        db.Notifications.AddRange(payment, package, unknown, otherPayment);
        await db.SaveChangesAsync();
        var service = new NotificationService(db, new ClientUser(user.Id), new NoAlerts());
        var filtered = await service.GetCurrentUserNotificationsAsync(1, " Payments ", false);
        Assert.Equal(payment.Id, Assert.Single(filtered).Id);
        Assert.Equal("payments", filtered[0].Category);
        Assert.Equal(1, (await service.GetUnreadCountAsync("payments")).UnreadCount);
        Assert.Equal(unknown.Id, Assert.Single(await service.GetCurrentUserNotificationsAsync(50, "system")).Id);
        await Assert.ThrowsAsync<ArgumentException>(() => service.MarkAllAsReadAsync("typo"));
        Assert.Equal(3, (await service.GetUnreadCountAsync()).UnreadCount);
        var counters = await service.GetUnreadCountAsync();
        Assert.Equal(1, counters.UnreadByCategory["payments"]);
        Assert.Equal(1, counters.UnreadByCategory["packages"]);
        Assert.Equal(1, counters.UnreadByCategory["system"]);
        Assert.Equal(0, counters.UnreadByCategory["trainers"]);
        Assert.Equal(1, (await service.MarkAllAsReadAsync("payments")).MarkedAsRead);
        Assert.Equal(0, (await service.MarkAllAsReadAsync("payments")).MarkedAsRead);
        Assert.Empty(await service.GetCurrentUserNotificationsAsync(50, "payments", false));
        Assert.Single(await service.GetCurrentUserNotificationsAsync(50, "payments", true));
        Assert.Equal(2, (await service.GetUnreadCountAsync()).UnreadCount);
        Assert.Equal("packages", (await service.MarkAsReadAsync(package.Id))!.Category);
        Assert.Null(await service.MarkAsReadAsync(otherPayment.Id));
        Assert.False(await db.Notifications.Where(x => x.Id == otherPayment.Id).Select(x => x.IsRead).SingleAsync());
    }

    private sealed class NoAlerts : IOperationalAlertService
    {
        public Task<OperationalAlertsDto> GetAlertsAsync(OperationalAlertFilterDto filter) => throw new InvalidOperationException("Client notifications must not synchronize staff alerts.");
    }

    private sealed class ClientUser(int id) : ICurrentUserService
    {
        public int? UserId => id;
        public string? Email => null;
        public List<string> Roles => new() { "Client" };
        public bool IsAuthenticated => true;
        public bool IsClient => true;
        public bool IsOwner => false;
        public bool IsTrainer => false;
    }
}
