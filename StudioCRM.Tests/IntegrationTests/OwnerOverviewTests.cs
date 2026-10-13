using Microsoft.EntityFrameworkCore;
using StudioCRM.Domain.Entities;
using StudioCRM.Domain.Enums;
using StudioCRM.Infrastructure.Services;

namespace StudioCRM.Tests.IntegrationTests;
public class OwnerOverviewTests
{
    private static readonly DateTime Now = new(2026, 10, 13, 10, 0, 0, DateTimeKind.Utc);
    private sealed class Clock : TimeProvider { public override DateTimeOffset GetUtcNow() => new(Now); }
    private static OwnerOverviewService Service(StudioCRM.Infrastructure.Persistence.StudioCRMDbContext db, string role = "Owner") => new(db, new PortalSecurityTests.Actor(1, role), new Clock());
    private static DateTime At(int month, int day) => new(2026, month, day, 9, 0, 0, DateTimeKind.Utc);

    [PostgresFact]
    public async Task SummaryAndRevenueUseRealCashFlowLocalMonthsAndDistinctActivity()
    {
        await using var database = await TestDatabase.CreateAsync(); await using var db = database.Context();
        var location = new Location { Name = "Dashboard" };
        var trainer = new Trainer { User = new User { Email = "dashboard@example.test" } };
        var a = new Client { FirstName = "Anna", Location = location, TrainingStartDate = At(10, 2).Date };
        var b = new Client { FirstName = "Jan", Location = location, TrainingStartDate = At(9, 2).Date };
        var c = new Client { FirstName = "Service", Location = location };
        var archived = new Client { FirstName = "Archived", Location = location, IsDeleted = true, DeletedAt = At(10, 12) };
        var october = new Session { Location = location, Trainer = trainer, Status = "Completed", StartAt = At(10, 5), EndAt = At(10, 5).AddHours(1) };
        db.AddRange(new SessionParticipant { Client = a, Session = october, AttendanceStatus = "Present" },
            new SessionParticipant { Client = b, Session = october, AttendanceStatus = "Present" },
            new SessionParticipant { Client = a, Session = new Session { Location = location, Trainer = trainer, Status = "Completed", StartAt = At(9, 5), EndAt = At(9, 5).AddHours(1) }, AttendanceStatus = "Present" });
        var offer = new Package { Name = "Diet", BillingMode = PackageBillingMode.PeriodBased, DurationDays = 30 };
        foreach (var client in new[] { a, c }) db.Add(new ClientPackage { Client = client, Package = offer, BillingMode = PackageBillingMode.PeriodBased,
            PeriodStartsAt = At(10, 1), ValidUntil = At(11, 1), PeriodDurationDays = 31, TotalSessions = 0 });
        // October in Poland despite September UTC. Archived client's payment must remain in revenue.
        db.Add(new ClientPayment { Client = archived, Amount = 100, PaymentDate = new DateTime(2026, 9, 30, 22, 10, 0, DateTimeKind.Utc), Status = ClientPaymentStatus.Confirmed });
        db.Add(new ClientPayment { Client = a, Amount = 50, PaymentDate = At(9, 15), Status = ClientPaymentStatus.Confirmed });
        db.Add(new ClientPayment { Client = a, Amount = 900, PaymentDate = At(10, 2), Status = ClientPaymentStatus.PendingConfirmation });
        db.Add(new ClientPayment { Client = a, Amount = 700, PaymentDate = At(10, 2), Status = ClientPaymentStatus.Reversed });
        db.Add(new ClientPayment { Client = a, Amount = 800, Currency = "EUR", PaymentDate = At(10, 2), Status = ClientPaymentStatus.Confirmed });
        db.Add(new ClientPackage { Client = b, Package = new Package { Name = "Refunded" }, IsActive = false, TotalSessions = 4,
            RefundConfirmedAt = At(10, 6), RefundAmount = 20, ClosureDisposition = "Refunded" });
        await db.SaveChangesAsync();
        var summary = await Service(db).SummaryAsync("2026-10");
        Assert.Equal(80, summary.Revenue.Current); Assert.Equal(50, summary.Revenue.Previous); Assert.Equal(60, summary.Revenue.ChangePercent);
        Assert.Equal(3, summary.ActiveClients.Current); Assert.Equal(1, summary.ActiveClients.Previous);
        Assert.Equal(1, summary.CompletedSessions.Current); Assert.Equal(1, summary.CompletedSessions.Previous);
        Assert.Equal(1, summary.NewClients); Assert.Equal(1, summary.ClientsWithoutJoinedAt); Assert.True(summary.IsPartial);
        var chart = await Service(db).RevenueAsync(6, "2026-10");
        Assert.Equal(6, chart.Items.Count); Assert.Equal("2026-05", chart.Items[0].Period);
        Assert.Equal(100, chart.Items[^1].Received); Assert.Equal(20, chart.Items[^1].Refunded); Assert.Equal(80, chart.Items[^1].Revenue);
        Assert.Equal(130, chart.Total); Assert.Equal(21.67m, chart.Average); Assert.Null(chart.YearOverYearChangePercent);
        Assert.All(chart.Items.Take(4), item => Assert.Equal(0, item.Revenue));
    }

    [PostgresFact]
    public async Task RecentClientsNeverInventJoiningDateFromCreationDate()
    {
        await using var database = await TestDatabase.CreateAsync(); await using var db = database.Context();
        var location = new Location { Name = "Recent" };
        db.AddRange(new Client { FirstName = "Unknown", Location = location, CreatedAt = Now },
            new Client { FirstName = "Joined", Location = location, TrainingStartDate = At(10, 12).Date, CreatedAt = At(9, 1) },
            new Client { FirstName = "Future", Location = location, TrainingStartDate = At(10, 20).Date });
        await db.SaveChangesAsync();
        var rows = await Service(db).RecentClientsAsync(4);
        Assert.Equal(2, rows.Count); Assert.Equal("Joined", rows[0].FirstName); Assert.Null(rows[1].JoinedAt);
        Assert.Equal(Now, rows[1].CreatedAt);
    }

    [PostgresFact]
    public async Task AttentionCountsClientsOnceAndExcludesPurePeriodServices()
    {
        await using var database = await TestDatabase.CreateAsync(); await using var db = database.Context();
        var location = new Location { Name = "Attention" };
        var trainer = new Trainer { User = new User { Email = "attention@example.test" } };
        var old = new Client { FirstName = "Old", Location = location, Status = "Active" };
        var never = new Client { FirstName = "Never", Location = location, Status = "Active", TrainingStartDate = At(8, 1).Date };
        var unknown = new Client { FirstName = "Unknown", Location = location, Status = "Active" };
        var diet = new Client { FirstName = "Diet only", Location = location, Status = "Active", TrainingStartDate = At(8, 1).Date };
        var offer = new Package { Name = "Training" };
        foreach (var client in new[] { old, never, unknown }) db.Add(new ClientPackage { Client = client, Package = offer, TotalSessions = 4, UsedSessions = client == old ? 3 : 0, ValidUntil = At(11, 1) });
        db.Add(new ClientPackage { Client = old, Package = new Package { Name = "Group", BillingType = SessionBillingType.Group }, ExpectedBillingType = SessionBillingType.Group, TotalSessions = 1, ValidUntil = At(11, 1) });
        db.Add(new ClientPackage { Client = diet, Package = new Package { Name = "Diet", BillingMode = PackageBillingMode.PeriodBased }, BillingMode = PackageBillingMode.PeriodBased,
            PeriodStartsAt = At(8, 1), ValidUntil = At(11, 1), PeriodDurationDays = 92, TotalSessions = 0 });
        foreach (var (day, status) in new[] { (At(9, 1), "Present"), (At(10, 12), "NoShow") })
            db.Add(new SessionParticipant { Client = old, AttendanceStatus = status, Session = new Session { Trainer = trainer, Location = location, StartAt = day, EndAt = day.AddHours(1), Status = "Completed" } });
        await db.SaveChangesAsync();
        var result = await Service(db).AttentionAsync();
        Assert.Equal(2, result.ClientsWithoutTraining30Days); Assert.Equal(1, result.ClientsWithOneRemainingSession);
        Assert.Equal(1, result.ClientsWithoutAttendanceBaseline);
    }

    [PostgresFact]
    public async Task RejectsNonOwnersAndInvalidRanges()
    {
        await using var database = await TestDatabase.CreateAsync(); await using var db = database.Context();
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => Service(db, "Trainer").SummaryAsync(null));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => Service(db, "Client").RevenueAsync(12, null));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => Service(db, "Trainer").RecentClientsAsync(4));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => Service(db, "Trainer").AttentionAsync());
        await Assert.ThrowsAsync<InvalidOperationException>(() => Service(db).SummaryAsync("2026-11"));
        await Assert.ThrowsAsync<InvalidOperationException>(() => Service(db).SummaryAsync("2026-1"));
        await Assert.ThrowsAsync<InvalidOperationException>(() => Service(db).RevenueAsync(120, null));
        await Assert.ThrowsAsync<InvalidOperationException>(() => Service(db).RecentClientsAsync(10000));
        Assert.Null(OwnerOverviewService.Percent(10, 0)); Assert.Equal(0, OwnerOverviewService.Percent(0, 0));
    }
}
