using Microsoft.EntityFrameworkCore;
using StudioCRM.Domain.Entities;
using StudioCRM.Domain.Enums;
namespace StudioCRM.Tests.IntegrationTests;
public class PaymentDateTests
{
    [PostgresFact]
    public async Task BackdatingAndCorrectionPreserveFinancialEntriesAndAuditReason()
    {
        await using var database = await TestDatabase.CreateAsync();
        await using var db = database.Context();
        var trainer = new Trainer { User = new User { Email = "date@test.invalid" } };
        var client = new Client { Trainer = trainer, Location = new Location { Name = "Date" } };
        db.Add(client); await db.SaveChangesAsync();
        var actor = new PortalSecurityTests.Actor(trainer.UserId);
        var service = PortalSecurityTests.Payments(db, actor);
        var firstDate = new DateTime(2026, 1, 5, 10, 0, 0, DateTimeKind.Utc);
        var original = await service.CreatePaymentAsStaffAsync(new() { ClientId = client.Id, Amount = 50, Method = PaymentMethod.Cash, PaymentDate = firstDate });
        Assert.Equal(firstDate, original.PaymentDate);
        db.ChangeTracker.Clear();
        var before = await db.ClientPayments.AsNoTracking().SingleAsync();
        var balance = await db.ClientBalanceTransactions.AsNoTracking().Select(t => new {t.Id,t.Amount,t.CreatedAt}).ToListAsync();
        var newDate = new DateTimeOffset(2025, 12, 31, 12, 0, 0, TimeSpan.FromHours(1));
        var request = new StudioCRM.Application.DTOs.Billing.UpdatePaymentDateRequest { PaymentDate = newDate, ExpectedPaymentDate = firstDate, Reason = "Late entry" };
        var result = await service.UpdatePaymentDateAsync(original.Id, request);
        Assert.Equal(newDate.UtcDateTime, result.PaymentDate);
        await service.UpdatePaymentDateAsync(original.Id, request);
        var after = await db.ClientPayments.AsNoTracking().SingleAsync();
        Assert.Equal(before.CreatedAt, after.CreatedAt); Assert.Equal(before.ConfirmedAt, after.ConfirmedAt);
        Assert.Equal(before.Amount, after.Amount); Assert.Equal(before.Status, after.Status);
        Assert.Equal(balance, await db.ClientBalanceTransactions.AsNoTracking().Select(t => new {t.Id,t.Amount,t.CreatedAt}).ToListAsync());
        var audit = Assert.Single(await db.ClientAuditEntries.Where(a => a.Action == "PaymentDateCorrected").ToListAsync());
        Assert.Equal("Late entry", audit.Reason); Assert.Equal(trainer.UserId, audit.ActorUserId);
        request.PaymentDate = newDate.AddDays(-1);
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => service.UpdatePaymentDateAsync(original.Id, request));
    }

    [PostgresFact]
    public async Task DateCorrectionRejectsForeignClientsGatewayAndFutureDates()
    {
        await using var database = await TestDatabase.CreateAsync(); await using var db = database.Context();
        var trainer = new Trainer { User = new User { Email = "date-access@test.invalid" } };
        var client = new Client { Location = new Location { Name = "Foreign" } };
        var date = new DateTime(2026, 1, 5, 10, 0, 0, DateTimeKind.Utc);
        var payment = new ClientPayment { Client = client, Amount = 50, PaymentDate = date, Status = ClientPaymentStatus.Confirmed, Source = ClientPaymentSource.PaymentGateway, Method = PaymentMethod.PaymentGateway };
        db.AddRange(trainer,payment); await db.SaveChangesAsync();
        var request = new StudioCRM.Application.DTOs.Billing.UpdatePaymentDateRequest { PaymentDate = date.AddDays(-1), ExpectedPaymentDate = date, Reason = "Correction" };
        await Assert.ThrowsAsync<InvalidOperationException>(() => PortalSecurityTests.Payments(db,new PortalSecurityTests.Actor(trainer.UserId)).UpdatePaymentDateAsync(payment.Id,request));
        await Assert.ThrowsAsync<InvalidOperationException>(() => PortalSecurityTests.Payments(db,new PortalSecurityTests.Actor(trainer.UserId,"Owner")).UpdatePaymentDateAsync(payment.Id,request));
        request.PaymentDate = DateTimeOffset.UtcNow.AddDays(1);
        await Assert.ThrowsAsync<InvalidOperationException>(() => PortalSecurityTests.Payments(db,new PortalSecurityTests.Actor(trainer.UserId,"Owner")).UpdatePaymentDateAsync(payment.Id,request));
        Assert.Equal(date,(await db.ClientPayments.AsNoTracking().SingleAsync()).PaymentDate);
        Assert.Empty(await db.ClientAuditEntries.ToListAsync());
    }
}
