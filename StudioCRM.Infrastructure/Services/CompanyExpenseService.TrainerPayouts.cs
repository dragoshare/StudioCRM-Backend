using Microsoft.EntityFrameworkCore;
using StudioCRM.Application.DTOs.Billing;
using StudioCRM.Domain.Entities;
using StudioCRM.Domain.Enums;

namespace StudioCRM.Infrastructure.Services;

public partial class CompanyExpenseService
{
    internal const string ZleceniePayoutsLabel = "Wynagrodzenia trenerów (umowy zlecenie)";

    // Trainers on umowa zlecenie issue no invoice, so their paid monthly settlement is the cost document.
    // Only settlements marked as paid count; the cost is dated to the last day of the worked month.
    // B2B trainers (and trainers with any B2B contract in that month) are left out: their invoices are entered
    // as company expenses and would otherwise be counted twice. The rows are statistics-only, never saved.
    private async Task<List<CompanyExpense>> PaidZlecenieSettlementCostsAsync(
        CompanyExpenseFilterDto filter,
        DateTime? from,
        DateTime? to)
    {
        // Settlements have no legal entity, location, due date, recurrence or document text to match.
        if (filter.LegalEntityId.HasValue || filter.LocationId.HasValue ||
            filter.DueFrom.HasValue || filter.DueTo.HasValue ||
            filter.IsRecurring == true || filter.RecurringGroupId != null ||
            filter.IsOverdue == true || !string.IsNullOrWhiteSpace(filter.Search) ||
            filter.Category is not (null or ExpenseCategory.TrainerCosts) ||
            filter.PaymentStatus is not (null or ExpensePaymentStatus.Paid))
            return new List<CompanyExpense>();

        var paidFrom = NormalizeNullableDateTime(filter.PaidFrom);
        var paidTo = NormalizeNullableDateTime(filter.PaidTo);
        var settlements = await _context.TrainerMonthlySettlements.AsNoTracking()
            .Where(s => s.IsPaid && s.TotalAmount != 0 &&
                        (!paidFrom.HasValue || s.PaidAt >= paidFrom) &&
                        (!paidTo.HasValue || s.PaidAt <= paidTo))
            .Select(s => new { s.Id, s.TrainerId, s.Year, s.Month, s.TotalAmount, s.PaidAt })
            .ToListAsync();
        settlements = settlements
            .Where(s => (!from.HasValue || MonthCostDate(s.Year, s.Month) >= from) &&
                        (!to.HasValue || MonthCostDate(s.Year, s.Month) <= to))
            .ToList();
        if (settlements.Count == 0)
            return new List<CompanyExpense>();

        var trainerIds = settlements.Select(s => s.TrainerId).Distinct().ToList();
        var contracts = await _context.TrainerContracts.AsNoTracking()
            .Where(c => trainerIds.Contains(c.TrainerId) && c.IsActive)
            .Select(c => new { c.TrainerId, c.ContractType, c.ValidFrom, c.ValidTo })
            .ToListAsync();
        var payouts = new LegalEntity { Name = ZleceniePayoutsLabel };

        return settlements
            .Where(s =>
            {
                var monthStart = new DateTime(s.Year, s.Month, 1, 0, 0, 0, DateTimeKind.Utc);
                var monthEnd = monthStart.AddMonths(1);
                var types = contracts
                    .Where(c => c.TrainerId == s.TrainerId && c.ValidFrom < monthEnd && (c.ValidTo == null || c.ValidTo >= monthStart))
                    .Select(c => c.ContractType)
                    .ToList();
                return types.Count > 0 && types.All(t => t == TrainerContractType.Zlecenie);
            })
            .Select(s => new CompanyExpense
            {
                LegalEntity = payouts,
                Category = ExpenseCategory.TrainerCosts,
                PaymentStatus = ExpensePaymentStatus.Paid,
                IssueDate = MonthCostDate(s.Year, s.Month),
                PaidAt = s.PaidAt,
                NetAmount = s.TotalAmount,
                VatAmount = 0,
                GrossAmount = s.TotalAmount
            })
            .ToList();
    }

    private static DateTime MonthCostDate(int year, int month) =>
        new(year, month, DateTime.DaysInMonth(year, month), 0, 0, 0, DateTimeKind.Utc);
}
