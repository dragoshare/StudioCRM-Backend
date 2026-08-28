using Microsoft.EntityFrameworkCore;
using StudioCRM.Application.DTOs.Billing;
using StudioCRM.Application.Interfaces;
using StudioCRM.Domain.Entities;
using StudioCRM.Domain.Enums;
using StudioCRM.Infrastructure.Persistence;

namespace StudioCRM.Infrastructure.Services;

public class CompanyExpenseService : ICompanyExpenseService
{
    private readonly StudioCRMDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public CompanyExpenseService(StudioCRMDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<PagedResultDto<CompanyExpenseDto>> GetExpensesAsync(CompanyExpenseFilterDto filter)
    {
        var query = ApplyFilters(BaseExpenseQuery(), filter);
        return await ToPagedResultAsync(query, filter);
    }

    public async Task<CompanyExpenseDto?> GetExpenseAsync(int id)
    {
        var expense = await BaseExpenseQuery()
            .FirstOrDefaultAsync(x => x.Id == id);

        return expense is null ? null : MapExpense(expense);
    }

    public async Task<CompanyExpenseDto> CreateExpenseAsync(CreateCompanyExpenseRequest request)
    {
        await EnsureExpenseContextAsync(request.LegalEntityId, request.LocationId);

        var paymentStatus = ResolvePaymentStatus(
            request.PaymentStatus,
            request.DueDate,
            request.PaidAt);

        var expense = new CompanyExpense
        {
            LegalEntityId = request.LegalEntityId,
            LocationId = request.LocationId,
            Category = request.Category,
            PaymentStatus = paymentStatus,
            VendorName = NormalizeRequiredText(request.VendorName, "Vendor name is required."),
            VendorNip = NormalizeOptionalText(request.VendorNip),
            InvoiceNumber = NormalizeOptionalText(request.InvoiceNumber),
            IssueDate = NormalizeDateTime(request.IssueDate),
            SaleDate = NormalizeNullableDateTime(request.SaleDate),
            DueDate = NormalizeNullableDateTime(request.DueDate),
            PaidAt = paymentStatus == ExpensePaymentStatus.Paid
                ? NormalizeNullableDateTime(request.PaidAt) ?? DateTime.UtcNow
                : null,
            NetAmount = NormalizeMoney(request.NetAmount, allowZero: true),
            VatAmount = NormalizeMoney(request.VatAmount, allowZero: true),
            GrossAmount = NormalizeMoney(request.GrossAmount, allowZero: false),
            Currency = NormalizeCurrency(request.Currency),
            Description = NormalizeOptionalText(request.Description),
            Notes = NormalizeOptionalText(request.Notes),
            AttachmentUrl = NormalizeOptionalText(request.AttachmentUrl),
            IsRecurring = request.IsRecurring,
            RecurringGroupId = NormalizeOptionalText(request.RecurringGroupId),
            CreatedByUserId = _currentUser.UserId,
            PaidByUserId = paymentStatus == ExpensePaymentStatus.Paid ? _currentUser.UserId : null,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        EnsureAmountConsistency(expense.NetAmount, expense.VatAmount, expense.GrossAmount);

        await _context.CompanyExpenses.AddAsync(expense);
        await _context.SaveChangesAsync();

        return await GetExpenseAsync(expense.Id)
            ?? throw new InvalidOperationException("Created expense could not be loaded.");
    }

    public async Task<CompanyExpenseDto?> UpdateExpenseAsync(int id, UpdateCompanyExpenseRequest request)
    {
        var expense = await _context.CompanyExpenses.FirstOrDefaultAsync(x => x.Id == id);

        if (expense is null)
            return null;

        await EnsureExpenseContextAsync(request.LegalEntityId, request.LocationId);

        var paymentStatus = ResolvePaymentStatus(
            request.PaymentStatus,
            request.DueDate,
            request.PaidAt);

        expense.LegalEntityId = request.LegalEntityId;
        expense.LocationId = request.LocationId;
        expense.Category = request.Category;
        expense.PaymentStatus = paymentStatus;
        expense.VendorName = NormalizeRequiredText(request.VendorName, "Vendor name is required.");
        expense.VendorNip = NormalizeOptionalText(request.VendorNip);
        expense.InvoiceNumber = NormalizeOptionalText(request.InvoiceNumber);
        expense.IssueDate = NormalizeDateTime(request.IssueDate);
        expense.SaleDate = NormalizeNullableDateTime(request.SaleDate);
        expense.DueDate = NormalizeNullableDateTime(request.DueDate);
        expense.PaidAt = paymentStatus == ExpensePaymentStatus.Paid
            ? NormalizeNullableDateTime(request.PaidAt) ?? expense.PaidAt ?? DateTime.UtcNow
            : null;
        expense.NetAmount = NormalizeMoney(request.NetAmount, allowZero: true);
        expense.VatAmount = NormalizeMoney(request.VatAmount, allowZero: true);
        expense.GrossAmount = NormalizeMoney(request.GrossAmount, allowZero: false);
        expense.Currency = NormalizeCurrency(request.Currency);
        expense.Description = NormalizeOptionalText(request.Description);
        expense.Notes = NormalizeOptionalText(request.Notes);
        expense.AttachmentUrl = NormalizeOptionalText(request.AttachmentUrl);
        expense.IsRecurring = request.IsRecurring;
        expense.RecurringGroupId = NormalizeOptionalText(request.RecurringGroupId);
        expense.PaidByUserId = paymentStatus == ExpensePaymentStatus.Paid
            ? expense.PaidByUserId ?? _currentUser.UserId
            : null;
        expense.UpdatedAt = DateTime.UtcNow;

        EnsureAmountConsistency(expense.NetAmount, expense.VatAmount, expense.GrossAmount);

        await _context.SaveChangesAsync();

        return await GetExpenseAsync(expense.Id);
    }

    public async Task<CompanyExpenseDto?> MarkPaidAsync(int id, DateTime? paidAt = null)
    {
        var expense = await _context.CompanyExpenses.FirstOrDefaultAsync(x => x.Id == id);

        if (expense is null)
            return null;

        if (expense.PaymentStatus == ExpensePaymentStatus.Cancelled)
            throw new InvalidOperationException("Cancelled expense cannot be marked as paid.");

        expense.PaymentStatus = ExpensePaymentStatus.Paid;
        expense.PaidAt = NormalizeNullableDateTime(paidAt) ?? DateTime.UtcNow;
        expense.PaidByUserId = _currentUser.UserId;
        expense.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        return await GetExpenseAsync(expense.Id);
    }

    public async Task<bool> DeleteExpenseAsync(int id)
    {
        var expense = await _context.CompanyExpenses.FirstOrDefaultAsync(x => x.Id == id);

        if (expense is null)
            return false;

        _context.CompanyExpenses.Remove(expense);
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<ExpenseStatisticsDto> GetStatisticsAsync(CompanyExpenseFilterDto filter)
    {
        var from = NormalizeNullableDateTime(filter.From);
        var to = NormalizeNullableDateTime(filter.To);
        var expenses = await ApplyFilters(BaseExpenseQuery(), filter).ToListAsync();
        var confirmedPaymentsQuery = _context.ClientPayments
            .Where(x => x.Status == ClientPaymentStatus.Confirmed);

        if (filter.LegalEntityId.HasValue)
            confirmedPaymentsQuery = confirmedPaymentsQuery.Where(x => x.LegalEntityId == filter.LegalEntityId.Value);

        if (filter.LocationId.HasValue)
            confirmedPaymentsQuery = confirmedPaymentsQuery.Where(x => x.LocationId == filter.LocationId.Value);

        if (from.HasValue)
            confirmedPaymentsQuery = confirmedPaymentsQuery.Where(x => x.PaymentDate >= from.Value);

        if (to.HasValue)
            confirmedPaymentsQuery = confirmedPaymentsQuery.Where(x => x.PaymentDate <= to.Value);

        var revenueGross = await confirmedPaymentsQuery.SumAsync(x => (decimal?)x.Amount) ?? 0;
        var financialExpenses = filter.PaymentStatus == ExpensePaymentStatus.Cancelled
            ? expenses
            : expenses.Where(x => x.PaymentStatus != ExpensePaymentStatus.Cancelled).ToList();
        var grossAmount = financialExpenses.Sum(x => x.GrossAmount);

        return new ExpenseStatisticsDto
        {
            From = from,
            To = to,
            ExpenseCount = financialExpenses.Count,
            PaidCount = financialExpenses.Count(x => x.PaymentStatus == ExpensePaymentStatus.Paid),
            UnpaidCount = financialExpenses.Count(x =>
                x.PaymentStatus == ExpensePaymentStatus.Unpaid ||
                x.PaymentStatus == ExpensePaymentStatus.Overdue),
            OverdueCount = financialExpenses.Count(IsOverdue),
            NetAmount = financialExpenses.Sum(x => x.NetAmount),
            VatAmount = financialExpenses.Sum(x => x.VatAmount),
            GrossAmount = grossAmount,
            PaidGrossAmount = financialExpenses
                .Where(x => x.PaymentStatus == ExpensePaymentStatus.Paid)
                .Sum(x => x.GrossAmount),
            UnpaidGrossAmount = financialExpenses
                .Where(x => x.PaymentStatus != ExpensePaymentStatus.Paid &&
                            x.PaymentStatus != ExpensePaymentStatus.Cancelled)
                .Sum(x => x.GrossAmount),
            OverdueGrossAmount = financialExpenses
                .Where(IsOverdue)
                .Sum(x => x.GrossAmount),
            RevenueGrossAmount = revenueGross,
            OperatingProfitGrossAmount = revenueGross - grossAmount,
            ByLegalEntity = financialExpenses
                .GroupBy(x => new { x.LegalEntityId, x.LegalEntity.Name })
                .Select(x => BuildBreakdown(x.Key.LegalEntityId.ToString(), x.Key.Name, x))
                .OrderByDescending(x => x.GrossAmount)
                .ToList(),
            ByLocation = financialExpenses
                .GroupBy(x => new
                {
                    Key = x.LocationId?.ToString() ?? "none",
                    Label = x.Location?.Name ?? "Bez lokalizacji"
                })
                .Select(x => BuildBreakdown(x.Key.Key, x.Key.Label, x))
                .OrderByDescending(x => x.GrossAmount)
                .ToList(),
            ByCategory = financialExpenses
                .GroupBy(x => x.Category)
                .Select(x => BuildBreakdown(((int)x.Key).ToString(), x.Key.ToString(), x))
                .OrderByDescending(x => x.GrossAmount)
                .ToList(),
            ByPaymentStatus = financialExpenses
                .GroupBy(x => x.PaymentStatus)
                .Select(x => BuildBreakdown(((int)x.Key).ToString(), x.Key.ToString(), x))
                .OrderByDescending(x => x.GrossAmount)
                .ToList(),
            ByMonth = financialExpenses
                .GroupBy(x => x.IssueDate.ToString("yyyy-MM"))
                .Select(x => BuildBreakdown(x.Key, x.Key, x))
                .OrderBy(x => x.Key)
                .ToList()
        };
    }

    private IQueryable<CompanyExpense> BaseExpenseQuery()
    {
        return _context.CompanyExpenses
            .Include(x => x.LegalEntity)
            .Include(x => x.Location)
            .AsQueryable();
    }

    private static IQueryable<CompanyExpense> ApplyFilters(
        IQueryable<CompanyExpense> query,
        CompanyExpenseFilterDto filter)
    {
        if (filter.LegalEntityId.HasValue)
            query = query.Where(x => x.LegalEntityId == filter.LegalEntityId.Value);

        if (filter.LocationId.HasValue)
            query = query.Where(x => x.LocationId == filter.LocationId.Value);

        if (filter.Category.HasValue)
            query = query.Where(x => x.Category == filter.Category.Value);

        if (filter.PaymentStatus.HasValue)
            query = query.Where(x => x.PaymentStatus == filter.PaymentStatus.Value);

        if (filter.From.HasValue)
        {
            var from = NormalizeNullableDateTime(filter.From)!.Value;
            query = query.Where(x => x.IssueDate >= from);
        }

        if (filter.To.HasValue)
        {
            var to = NormalizeNullableDateTime(filter.To)!.Value;
            query = query.Where(x => x.IssueDate <= to);
        }

        if (filter.DueFrom.HasValue)
        {
            var dueFrom = NormalizeNullableDateTime(filter.DueFrom)!.Value;
            query = query.Where(x => x.DueDate >= dueFrom);
        }

        if (filter.DueTo.HasValue)
        {
            var dueTo = NormalizeNullableDateTime(filter.DueTo)!.Value;
            query = query.Where(x => x.DueDate <= dueTo);
        }

        if (filter.PaidFrom.HasValue)
        {
            var paidFrom = NormalizeNullableDateTime(filter.PaidFrom)!.Value;
            query = query.Where(x => x.PaidAt >= paidFrom);
        }

        if (filter.PaidTo.HasValue)
        {
            var paidTo = NormalizeNullableDateTime(filter.PaidTo)!.Value;
            query = query.Where(x => x.PaidAt <= paidTo);
        }

        if (filter.IsRecurring.HasValue)
            query = query.Where(x => x.IsRecurring == filter.IsRecurring.Value);

        if (filter.IsOverdue.HasValue)
        {
            var now = DateTime.UtcNow;
            query = filter.IsOverdue.Value
                ? query.Where(x =>
                    x.PaymentStatus != ExpensePaymentStatus.Paid &&
                    x.PaymentStatus != ExpensePaymentStatus.Cancelled &&
                    x.DueDate.HasValue &&
                    x.DueDate.Value < now)
                : query.Where(x =>
                    x.PaymentStatus == ExpensePaymentStatus.Paid ||
                    x.PaymentStatus == ExpensePaymentStatus.Cancelled ||
                    !x.DueDate.HasValue ||
                    x.DueDate.Value >= now);
        }

        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var search = filter.Search.Trim().ToLower();
            query = query.Where(x =>
                x.VendorName.ToLower().Contains(search) ||
                (x.VendorNip != null && x.VendorNip.ToLower().Contains(search)) ||
                (x.InvoiceNumber != null && x.InvoiceNumber.ToLower().Contains(search)) ||
                (x.Description != null && x.Description.ToLower().Contains(search)));
        }

        return query;
    }

    private static async Task<PagedResultDto<CompanyExpenseDto>> ToPagedResultAsync(
        IQueryable<CompanyExpense> query,
        CompanyExpenseFilterDto filter)
    {
        var page = NormalizePage(filter.Page);
        var pageSize = NormalizePageSize(filter.PageSize);

        var totalCount = await query.CountAsync();
        var expenses = await query
            .OrderByDescending(x => x.IssueDate)
            .ThenByDescending(x => x.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return new PagedResultDto<CompanyExpenseDto>
        {
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount,
            TotalPages = ResolveTotalPages(totalCount, pageSize),
            Items = expenses.Select(MapExpense).ToList()
        };
    }

    private async Task EnsureExpenseContextAsync(int legalEntityId, int? locationId)
    {
        var legalEntityExists = await _context.LegalEntities
            .AnyAsync(x => x.Id == legalEntityId && x.IsActive);

        if (!legalEntityExists)
            throw new InvalidOperationException("Legal entity does not exist or is inactive.");

        if (!locationId.HasValue)
            return;

        var location = await _context.Locations.FirstOrDefaultAsync(x => x.Id == locationId.Value);

        if (location is null)
            throw new InvalidOperationException("Location does not exist.");

        if (!location.IsActive)
            throw new InvalidOperationException("Location is inactive.");

        if (location.LegalEntityId.HasValue && location.LegalEntityId != legalEntityId)
            throw new InvalidOperationException("Expense legal entity must match the location legal entity.");
    }

    private static CompanyExpenseDto MapExpense(CompanyExpense expense)
    {
        return new CompanyExpenseDto
        {
            Id = expense.Id,
            LegalEntityId = expense.LegalEntityId,
            LegalEntityName = expense.LegalEntity.Name,
            LocationId = expense.LocationId,
            LocationName = expense.Location?.Name,
            Category = expense.Category,
            PaymentStatus = expense.PaymentStatus,
            VendorName = expense.VendorName,
            VendorNip = expense.VendorNip,
            InvoiceNumber = expense.InvoiceNumber,
            IssueDate = expense.IssueDate,
            SaleDate = expense.SaleDate,
            DueDate = expense.DueDate,
            PaidAt = expense.PaidAt,
            NetAmount = expense.NetAmount,
            VatAmount = expense.VatAmount,
            GrossAmount = expense.GrossAmount,
            Currency = expense.Currency,
            Description = expense.Description,
            Notes = expense.Notes,
            AttachmentUrl = expense.AttachmentUrl,
            IsRecurring = expense.IsRecurring,
            RecurringGroupId = expense.RecurringGroupId,
            CreatedByUserId = expense.CreatedByUserId,
            PaidByUserId = expense.PaidByUserId,
            IsOverdue = IsOverdue(expense),
            CreatedAt = expense.CreatedAt,
            UpdatedAt = expense.UpdatedAt
        };
    }

    private static ExpenseBreakdownDto BuildBreakdown(
        string key,
        string label,
        IEnumerable<CompanyExpense> expenses)
    {
        var items = expenses.ToList();

        return new ExpenseBreakdownDto
        {
            Key = key,
            Label = label,
            Count = items.Count,
            NetAmount = items.Sum(x => x.NetAmount),
            VatAmount = items.Sum(x => x.VatAmount),
            GrossAmount = items.Sum(x => x.GrossAmount),
            PaidGrossAmount = items
                .Where(x => x.PaymentStatus == ExpensePaymentStatus.Paid)
                .Sum(x => x.GrossAmount),
            UnpaidGrossAmount = items
                .Where(x => x.PaymentStatus != ExpensePaymentStatus.Paid &&
                            x.PaymentStatus != ExpensePaymentStatus.Cancelled)
                .Sum(x => x.GrossAmount)
        };
    }

    private static bool IsOverdue(CompanyExpense expense)
    {
        return expense.PaymentStatus != ExpensePaymentStatus.Paid &&
            expense.PaymentStatus != ExpensePaymentStatus.Cancelled &&
            expense.DueDate.HasValue &&
            expense.DueDate.Value < DateTime.UtcNow;
    }

    private static ExpensePaymentStatus ResolvePaymentStatus(
        ExpensePaymentStatus requestedStatus,
        DateTime? dueDate,
        DateTime? paidAt)
    {
        if (requestedStatus == ExpensePaymentStatus.Cancelled)
            return ExpensePaymentStatus.Cancelled;

        if (requestedStatus == ExpensePaymentStatus.Paid || paidAt.HasValue)
            return ExpensePaymentStatus.Paid;

        var normalizedDueDate = NormalizeNullableDateTime(dueDate);
        return normalizedDueDate.HasValue && normalizedDueDate.Value < DateTime.UtcNow
            ? ExpensePaymentStatus.Overdue
            : ExpensePaymentStatus.Unpaid;
    }

    private static void EnsureAmountConsistency(decimal netAmount, decimal vatAmount, decimal grossAmount)
    {
        if (netAmount == 0 && vatAmount == 0)
            return;

        var expectedGross = decimal.Round(netAmount + vatAmount, 2);

        if (Math.Abs(expectedGross - grossAmount) > 0.01m)
            throw new InvalidOperationException("Gross amount must match net amount plus VAT amount.");
    }

    private static decimal NormalizeMoney(decimal amount, bool allowZero)
    {
        if (amount < 0 || (!allowZero && amount <= 0))
            throw new InvalidOperationException("Amount must be greater than zero.");

        return decimal.Round(amount, 2);
    }

    private static string NormalizeCurrency(string? currency)
    {
        var normalized = NormalizeOptionalText(currency)?.ToUpperInvariant() ?? "PLN";

        if (normalized.Length != 3)
            throw new InvalidOperationException("Currency must use a 3-letter code.");

        return normalized;
    }

    private static string NormalizeRequiredText(string? value, string errorMessage)
    {
        var normalized = value?.Trim();

        if (string.IsNullOrWhiteSpace(normalized))
            throw new InvalidOperationException(errorMessage);

        return normalized;
    }

    private static string? NormalizeOptionalText(string? value)
    {
        var normalized = value?.Trim();
        return string.IsNullOrWhiteSpace(normalized) ? null : normalized;
    }

    private static DateTime NormalizeDateTime(DateTime value)
    {
        if (value == default)
            throw new InvalidOperationException("Issue date is required.");

        return value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
        };
    }

    private static DateTime? NormalizeNullableDateTime(DateTime? value)
    {
        if (!value.HasValue)
            return null;

        return value.Value.Kind switch
        {
            DateTimeKind.Utc => value.Value,
            DateTimeKind.Local => value.Value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value.Value, DateTimeKind.Utc)
        };
    }

    private static int NormalizePage(int page)
    {
        return Math.Max(1, page);
    }

    private static int NormalizePageSize(int pageSize)
    {
        return Math.Clamp(pageSize, 1, 100);
    }

    private static int ResolveTotalPages(int totalCount, int pageSize)
    {
        if (totalCount == 0)
            return 0;

        return (int)Math.Ceiling(totalCount / (decimal)pageSize);
    }
}
