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

    [PostgresFact]
    public async Task LocationUsesRecordedActivityAndMoneyWithoutGuessingMissingAssignments()
    {
        await using var database = await TestDatabase.CreateAsync(); await using var db = database.Context();
        var a = new Location { Name = "Klaj" }; var b = new Location { Name = "Niepolomice" };
        var client = new Client { FirstName = "Visitor", Location = a, TrainingStartDate = At(8, 1), Status = "Active" };
        var diet = new Client { FirstName = "Diet", Location = b, TrainingStartDate = At(10, 2), Status = "Active" };
        var trainer = new Trainer { User = new User { Email = "location-dashboard@test.example" } };
        foreach (var (location, date) in new[] { (a, At(9, 1)), (b, At(10, 5)) })
            db.Add(new SessionParticipant { Client = client, AttendanceStatus = "Present", Session = new Session {
                Trainer = trainer, Location = location, StartAt = date, EndAt = date.AddHours(1), Status = "Completed" } });
        var pass = new ClientPackage { Client = client, Package = new Package { Name = "Group", BillingType = SessionBillingType.Group },
            ExpectedBillingType = SessionBillingType.Group, Location = b, TotalSessions = 4, UsedSessions = 3, ValidUntil = At(11, 1) };
        db.Add(pass);
        db.Add(new ClientPackage { Client = diet, Package = new Package { Name = "Diet", BillingMode = PackageBillingMode.PeriodBased },
            Location = b, BillingMode = PackageBillingMode.PeriodBased, TotalSessions = 0, PeriodStartsAt = At(10, 1), ValidUntil = At(11, 1), PeriodDurationDays = 31 });
        db.Add(new ClientPackage { Client = diet, Package = new Package { Name = "Refund" }, Location = b,
            IsActive = false, RefundConfirmedAt = At(10, 6), RefundAmount = 20 });
        foreach (var (location, amount) in new (Location?, decimal)[] { (a, 100), (b, 200), (null, 50) })
            db.Add(new ClientPayment { Client = client, ClientPackage = pass, Location = location, Amount = amount,
                PaymentDate = At(10, 2), Status = ClientPaymentStatus.Confirmed });
        db.Add(new ClientPayment { Client = client, Location = a, Amount = 50, PaymentDate = At(9, 2), Status = ClientPaymentStatus.Confirmed });
        await db.SaveChangesAsync();
        var service = Service(db);
        var all = await service.SummaryAsync("2026-10");
        var left = await service.SummaryAsync("2026-10", a.Id);
        var right = await service.SummaryAsync("2026-10", b.Id);
        Assert.Equal(330, all.Revenue.Current); Assert.Equal(100, left.Revenue.Current); Assert.Equal(180, right.Revenue.Current);
        Assert.Equal(50, left.Revenue.Previous); Assert.Equal(100, left.Revenue.ChangePercent);
        Assert.Equal(0, left.ActiveClients.Current); Assert.Equal(2, right.ActiveClients.Current);
        Assert.Equal(0, left.CompletedSessions.Current); Assert.Equal(1, right.CompletedSessions.Current);
        Assert.Equal(0, left.NewClients); Assert.Equal(1, right.NewClients);
        var chart = await service.RevenueAsync(6, "2026-10", b.Id);
        Assert.Equal(180, chart.Total); Assert.Equal(180, chart.Items[^1].Revenue);
        Assert.Equal(diet.Id, Assert.Single(await service.RecentClientsAsync(4, b.Id)).Id);
        Assert.Equal(client.Id, Assert.Single(await service.RecentClientsAsync(4, a.Id)).Id);
        Assert.Equal(1, (await service.AttentionAsync(a.Id)).ClientsWithoutTraining30Days);
        Assert.Equal(0, (await service.AttentionAsync()).ClientsWithoutTraining30Days);
        Assert.Equal(0, (await service.AttentionAsync(a.Id)).ClientsWithOneRemainingSession);
        Assert.Equal(1, (await service.AttentionAsync(b.Id)).ClientsWithOneRemainingSession);
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.SummaryAsync(null, 0));
        await Assert.ThrowsAsync<KeyNotFoundException>(() => service.SummaryAsync(null, int.MaxValue));
        await Assert.ThrowsAsync<KeyNotFoundException>(() => service.RevenueAsync(6, null, int.MaxValue));
        await Assert.ThrowsAsync<KeyNotFoundException>(() => service.RecentClientsAsync(4, int.MaxValue));
        await Assert.ThrowsAsync<KeyNotFoundException>(() => service.AttentionAsync(int.MaxValue));
    }
}
