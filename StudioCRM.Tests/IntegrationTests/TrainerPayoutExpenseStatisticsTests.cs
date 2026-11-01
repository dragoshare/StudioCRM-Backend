using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using StudioCRM.Application.DTOs.Billing;
using StudioCRM.Application.Settings;
using StudioCRM.Domain.Entities;
using StudioCRM.Domain.Enums;
using StudioCRM.Infrastructure.Services;

namespace StudioCRM.Tests.IntegrationTests;

public class TrainerPayoutExpenseStatisticsTests
{
    [PostgresFact]
    public async Task PaidZlecenieSettlementsAreCostsOfTheWorkedMonthButB2BAndUnpaidAreNot()
    {
        await using var database = await TestDatabase.CreateAsync();
        await using var db = database.Context();
        Trainer Trainer(string email, params TrainerContractType[] types)
        {
            var trainer = new Trainer { User = new User { Email = email } };
            foreach (var type in types)
                db.Add(new TrainerContract { Trainer = trainer, ContractType = type, ContractNumber = email + type,
                    ValidFrom = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc) });
            return trainer;
        }
        var zlecenie = Trainer("zlecenie@test.invalid", TrainerContractType.Zlecenie);
        var b2b = Trainer("b2b@test.invalid", TrainerContractType.B2B);
        var mixed = Trainer("mixed@test.invalid", TrainerContractType.Zlecenie, TrainerContractType.B2B);
        var unpaid = Trainer("unpaid@test.invalid", TrainerContractType.Zlecenie);
        var paidAt = new DateTime(2026, 11, 5, 10, 0, 0, DateTimeKind.Utc);
        TrainerMonthlySettlement Settlement(Trainer trainer, int month, decimal amount, bool paid) => new()
            { Trainer = trainer, Year = 2026, Month = month, TotalAmount = amount, IsPaid = paid, PaidAt = paid ? paidAt : null };
        db.AddRange(Settlement(zlecenie, 10, 2943m, true), Settlement(zlecenie, 9, 1000m, true),
            Settlement(b2b, 10, 5000m, true), Settlement(mixed, 10, 700m, true), Settlement(unpaid, 10, 300m, false));
        db.Add(new CompanyExpense { LegalEntity = new LegalEntity { Name = "Studio" }, Category = ExpenseCategory.TaxesAndFees,
            VendorName = "US", PaymentStatus = ExpensePaymentStatus.Paid, IssueDate = new DateTime(2026, 10, 10, 0, 0, 0, DateTimeKind.Utc),
            NetAmount = 853.40m, GrossAmount = 853.40m });
        await db.SaveChangesAsync();
        var service = new CompanyExpenseService(db, new PortalSecurityTests.Actor(1, "Owner"), null!,
            Options.Create(new CloudflareR2Settings()), NullLogger<CompanyExpenseService>.Instance);
        var october = new CompanyExpenseFilterDto
        {
            From = new DateTime(2026, 10, 3, 0, 0, 0, DateTimeKind.Utc),
            To = new DateTime(2026, 11, 1, 0, 0, 0, DateTimeKind.Utc)
        };

        var stats = await service.GetStatisticsAsync(october);
        Assert.Equal(853.40m + 2943m, stats.GrossAmount);
        Assert.Equal(-stats.GrossAmount, stats.OperatingProfitNetAmount);
        Assert.Equal(2943m, stats.ByCategory.Single(x => x.Key == ((int)ExpenseCategory.TrainerCosts).ToString()).PaidGrossAmount);
        Assert.Equal(853.40m + 2943m, stats.ByMonth.Single(x => x.Key == "2026-10").GrossAmount);

        Assert.Equal(0m, (await service.GetStatisticsAsync(new CompanyExpenseFilterDto
            { From = october.From, To = october.To, PaymentStatus = ExpensePaymentStatus.Unpaid })).GrossAmount);
        Assert.Equal(853.40m, (await service.GetStatisticsAsync(new CompanyExpenseFilterDto
            { From = october.From, To = october.To, Category = ExpenseCategory.TaxesAndFees })).GrossAmount);
    }
}
