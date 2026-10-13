using System.Globalization;
using Microsoft.EntityFrameworkCore;
using StudioCRM.Application.DTOs.Dashboard;
using StudioCRM.Application.Interfaces;
using StudioCRM.Domain.Enums;
using StudioCRM.Infrastructure.Persistence;

namespace StudioCRM.Infrastructure.Services;

public class OwnerOverviewService(StudioCRMDbContext db, ICurrentUserService current, TimeProvider clock)
{
    private static TimeZoneInfo Zone => TimeZoneInfo.FindSystemTimeZoneById("Europe/Warsaw");
    private DateTime Now => clock.GetUtcNow().UtcDateTime;
    private static DateTime Utc(DateTime local) => TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(local, DateTimeKind.Unspecified), Zone);
    private static string Label(DateTime month) => month.ToString("yyyy-MM", CultureInfo.InvariantCulture);
    private void Owner() { if (!current.IsOwner) throw new UnauthorizedAccessException("Owner required."); }
    private async Task ValidateLocationAsync(int? locationId)
    {
        if (locationId <= 0) throw new InvalidOperationException("LocationId must be positive.");
        if (locationId.HasValue && !await db.Locations.AnyAsync(l => l.Id == locationId.Value))
            throw new KeyNotFoundException("Location not found.");
    }
    private DateTime Month(string? period, DateTime now)
    {
        var localNow = TimeZoneInfo.ConvertTimeFromUtc(now, Zone);
        var thisMonth = new DateTime(localNow.Year, localNow.Month, 1);
        if (period == null) return thisMonth;
        if (!DateTime.TryParseExact(period, "yyyy-MM", CultureInfo.InvariantCulture, DateTimeStyles.None, out var result) ||
            result.Year < 2000 || result > thisMonth)
            throw new InvalidOperationException("Period must be yyyy-MM, from 2000-01 through the current month.");
        return result;
    }
    public static decimal? Percent(decimal value, decimal previous) => previous == 0 ? value == 0 ? 0 : null : decimal.Round((value - previous) / Math.Abs(previous) * 100, 2);
    private static DashboardMoneyMetric Money(decimal value, decimal previous) => new(value, previous, value - previous, Percent(value, previous));
    private static DashboardCountMetric Count(int value, int previous) => new(value, previous, value - previous, Percent(value, previous));

    public async Task<OwnerOverviewSummaryDto> SummaryAsync(string? period, int? locationId = null)
    {
        Owner(); await ValidateLocationAsync(locationId); var now = Now; var month = Month(period, now); var start = Utc(month); var previous = month.AddMonths(-1);
        var end = Utc(month.AddMonths(1)); var partial = end > now; if (partial) end = now;
        var receipts = await RevenueRowsAsync(Utc(previous), end, locationId);
        decimal Revenue(string key) => receipts.Where(x => x.Period == key).Sum(x => x.Received - x.Refunded);
        var active = await ActiveClientsAsync(start, end, locationId);
        var previousActive = await ActiveClientsAsync(Utc(previous), start, locationId);
        var completed = await CompletedAsync(start, end, locationId);
        var previousCompleted = await CompletedAsync(Utc(previous), start, locationId);
        // TrainingStartDate is a business calendar date stored at UTC midnight, not an event timestamp.
        var dateStart = DateTime.SpecifyKind(month, DateTimeKind.Utc);
        var dateEnd = DateTime.SpecifyKind(partial ? TimeZoneInfo.ConvertTimeFromUtc(now, Zone).Date.AddDays(1) : month.AddMonths(1), DateTimeKind.Utc);
        var newClients = await db.Clients.IgnoreQueryFilters().CountAsync(c => (!locationId.HasValue || c.LocationId == locationId) && c.TrainingStartDate >= dateStart && c.TrainingStartDate < dateEnd);
        var missing = await db.Clients.CountAsync(c => (!locationId.HasValue || c.LocationId == locationId) && c.TrainingStartDate == null);
        return new(Label(month), Label(previous), Zone.Id, now, partial, "PLN", Money(Revenue(Label(month)), Revenue(Label(previous))),
            Count(active, previousActive), newClients, Count(completed, previousCompleted), missing);
    }
    private Task<int> CompletedAsync(DateTime start, DateTime end, int? locationId) => db.Sessions.IgnoreQueryFilters()
        .CountAsync(s => (!locationId.HasValue || s.LocationId == locationId) && !s.IsDeleted && s.Status == "Completed" && s.StartAt >= start && s.StartAt < end);
    private Task<int> ActiveClientsAsync(DateTime start, DateTime end, int? locationId)
    {
        var attendees = db.SessionParticipants.IgnoreQueryFilters().Where(p => (!locationId.HasValue || p.Session.LocationId == locationId) && !p.Session.IsDeleted && p.Session.Status == "Completed" &&
            p.AttendanceStatus == "Present" && p.Session.StartAt >= start && p.Session.StartAt < end).Select(p => p.ClientId);
        var services = db.ClientPackages.IgnoreQueryFilters().Where(p => (!locationId.HasValue || p.LocationId == locationId) && p.BillingMode == PackageBillingMode.PeriodBased &&
            p.PeriodStartsAt < end && p.ValidUntil > start && (p.ClosedAt == null || p.ClosedAt > start && p.ClosedAt > p.PeriodStartsAt) &&
            (!p.Client.IsDeleted || p.Client.DeletedAt > start && p.Client.DeletedAt > p.PeriodStartsAt)).Select(p => p.ClientId);
        return attendees.Union(services).CountAsync();
    }
    public async Task<OwnerRevenueDto> RevenueAsync(int months, string? period, int? locationId = null)
    {
        Owner(); await ValidateLocationAsync(locationId); if (months is not (6 or 12)) throw new InvalidOperationException("Months must be 6 or 12.");
        var now = Now; var month = Month(period, now); var from = month.AddMonths(1 - months); var end = Utc(month.AddMonths(1));
        if (end > now) end = now;
        var rows = await RevenueRowsAsync(Utc(from), end, locationId);
        var previousEnd = Utc(TimeZoneInfo.ConvertTimeFromUtc(end, Zone).AddYears(-1));
        var previousRows = await RevenueRowsAsync(Utc(from.AddYears(-1)), previousEnd, locationId);
        var items = Enumerable.Range(0, months).Select(i => {
            var m = from.AddMonths(i); var row = rows.SingleOrDefault(x => x.Period == Label(m));
            var received = row?.Received ?? 0; var refunded = row?.Refunded ?? 0;
            return new MonthlyRevenueDto(Label(m), received, refunded, received - refunded, Utc(m.AddMonths(1)) > now);
        }).ToList();
        var total = items.Sum(x => x.Revenue); var previousTotal = previousRows.Sum(x => x.Received - x.Refunded);
        return new(Label(month), months, "PLN", Zone.Id, now, items, total, decimal.Round(total / months, 2), previousTotal, Percent(total, previousTotal));
    }
    public sealed class RevenueRow
    {
        public string Period { get; set; } = "";
        public decimal Received { get; set; }
        public decimal Refunded { get; set; }
    }
    private Task<List<RevenueRow>> RevenueRowsAsync(DateTime start, DateTime end, int? locationId) => db.Database.SqlQuery<RevenueRow>($"""
        SELECT "Period", SUM("Received") AS "Received", SUM("Refunded") AS "Refunded"
        FROM (
          SELECT to_char("PaymentDate" AT TIME ZONE 'Europe/Warsaw', 'YYYY-MM') AS "Period",
                 "Amount" AS "Received", 0::numeric AS "Refunded"
          FROM "ClientPayments" WHERE (CAST({locationId} AS integer) IS NULL OR "LocationId" = {locationId}) AND "Status" = {(int)ClientPaymentStatus.Confirmed} AND "Currency" = 'PLN'
            AND "PaymentDate" >= {start} AND "PaymentDate" < {end}
          UNION ALL
          SELECT to_char("RefundConfirmedAt" AT TIME ZONE 'Europe/Warsaw', 'YYYY-MM') AS "Period",
                 0::numeric AS "Received", "RefundAmount" AS "Refunded"
          FROM "ClientPackages" WHERE (CAST({locationId} AS integer) IS NULL OR "LocationId" = {locationId}) AND "RefundConfirmedAt" IS NOT NULL AND "Currency" = 'PLN'
            AND "RefundConfirmedAt" >= {start} AND "RefundConfirmedAt" < {end}
        ) receipts GROUP BY "Period"
        """).ToListAsync();

    public async Task<List<RecentOwnerClientDto>> RecentClientsAsync(int limit, int? locationId = null)
    {
        Owner(); await ValidateLocationAsync(locationId); if (limit < 1 || limit > 20) throw new InvalidOperationException("Limit must be 1..20.");
        var today = DateTime.SpecifyKind(TimeZoneInfo.ConvertTimeFromUtc(Now, Zone).Date, DateTimeKind.Utc);
        return await db.Clients.AsNoTracking().Where(c => (!locationId.HasValue || c.LocationId == locationId) && (c.TrainingStartDate == null || c.TrainingStartDate <= today))
            .OrderByDescending(c => c.TrainingStartDate.HasValue).ThenByDescending(c => c.TrainingStartDate).ThenByDescending(c => c.CreatedAt).ThenByDescending(c => c.Id)
            .Take(limit).Select(c => new RecentOwnerClientDto(c.Id, c.FirstName, c.LastName, c.FirstName + " " + c.LastName, c.TrainingStartDate, c.CreatedAt)).ToListAsync();
    }
    public async Task<OwnerAttentionDto> AttentionAsync(int? locationId = null)
    {
        Owner(); await ValidateLocationAsync(locationId); var now = Now; var cutoff = Utc(TimeZoneInfo.ConvertTimeFromUtc(now, Zone).AddDays(-30));
        var cutoffDate = DateTime.SpecifyKind(TimeZoneInfo.ConvertTimeFromUtc(cutoff, Zone).Date, DateTimeKind.Utc);
        var eligible = db.Clients.Where(c => (!locationId.HasValue || c.LocationId == locationId) && c.Status != "Blocked" &&
            (db.ClientPackages.Any(p => p.ClientId == c.Id && (!locationId.HasValue || p.LocationId == locationId) && p.BillingMode == PackageBillingMode.SessionBased) ||
             db.SessionParticipants.Any(p => p.ClientId == c.Id && (!locationId.HasValue || p.Session.LocationId == locationId) && p.AttendanceStatus == "Present" && p.Session.Status == "Completed" && p.Session.StartAt < now)));
        var last = eligible.Select(c => new { c.TrainingStartDate, Last = db.SessionParticipants.Where(p => p.ClientId == c.Id && (!locationId.HasValue || p.Session.LocationId == locationId) &&
            p.AttendanceStatus == "Present" && p.Session.Status == "Completed" && p.Session.StartAt < now).Max(p => (DateTime?)p.Session.StartAt) });
        var inactive = await last.CountAsync(c => c.Last != null ? c.Last <= cutoff : c.TrainingStartDate != null && c.TrainingStartDate <= cutoffDate);
        var unknown = await last.CountAsync(c => c.Last == null && c.TrainingStartDate == null);
        var oneLeft = await db.ClientPackages.Where(p => (!locationId.HasValue || p.LocationId == locationId) && p.BillingMode == PackageBillingMode.SessionBased && p.IsActive && p.ClosureDisposition == null &&
            (p.ValidUntil == null || p.ValidUntil > now) && p.TotalSessions - p.UsedSessions == 1 && p.Client.Status != "Blocked")
            .Select(p => p.ClientId).Distinct().CountAsync();
        return new(now, 30, inactive, oneLeft, unknown);
    }
}
