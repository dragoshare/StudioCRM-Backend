using Microsoft.EntityFrameworkCore;
using StudioCRM.Application.DTOs.Billing;
using StudioCRM.Application.Interfaces;
using StudioCRM.Domain.Entities;
using StudioCRM.Domain.Enums;
using StudioCRM.Infrastructure.Persistence;
using System.Text.Json;

namespace StudioCRM.Infrastructure.Services;

public class PeriodPackageService(StudioCRMDbContext db, ICurrentUserService current, IStudioSettingsService settings)
{
    private static TimeZoneInfo Zone => TimeZoneInfo.FindSystemTimeZoneById("Europe/Warsaw");
    public static DateOnly Today() => DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, Zone));
    public static DateTime StartOfDay(DateOnly date) => TimeZoneInfo.ConvertTimeToUtc(date.ToDateTime(TimeOnly.MinValue), Zone);
    public static DateTime AddDays(DateTime start, int days) => StartOfDay(DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(start, Zone)).AddDays(days));

    public async Task<PagedResultDto<ClientPackageBillingDto>> ListAsync(int clientId, int page, int pageSize)
    {
        if (!current.IsOwner && !current.IsClient) throw new UnauthorizedAccessException("Owner or client required.");
        if (!await db.Clients.AnyAsync(c => c.Id == clientId && (current.IsOwner || c.UserId == current.UserId)))
            throw new KeyNotFoundException("Client not found.");
        if (page < 1 || page > 100000 || pageSize < 1 || pageSize > 100) throw new InvalidOperationException("Invalid pagination.");
        var q = db.ClientPackages.AsNoTracking().Where(p => p.ClientId == clientId && p.BillingMode == PackageBillingMode.PeriodBased);
        var total = await q.CountAsync();
        var rows = await q.Include(p => p.Client).Include(p => p.Location).ThenInclude(l => l!.LegalEntity)
            .OrderByDescending(p => p.PeriodStartsAt).ThenByDescending(p => p.Id).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();
        return new() { Items = rows.Select(p => ClientPaymentService.MapPackage(p)).ToList(), Page = page, PageSize = pageSize,
            TotalCount = total, TotalPages = (int)Math.Ceiling(total / (double)pageSize) };
    }

    public async Task SetRenewalAsync(int clientId, int id, bool enabled)
    {
        if (!current.IsOwner) throw new UnauthorizedAccessException("Owner required.");
        await using var tx = await db.Database.BeginTransactionAsync();
        await LockAsync(clientId, id);
        var p = await db.ClientPackages.SingleOrDefaultAsync(p => p.Id == id && p.ClientId == clientId && p.BillingMode == PackageBillingMode.PeriodBased)
            ?? throw new KeyNotFoundException("Period package not found.");
        if (!p.IsActive || p.ClosureDisposition != null || p.ValidUntil <= DateTime.UtcNow)
            throw new InvalidOperationException("Renewal can only be changed for an open, unexpired period.");
        if (p.PeriodAutoRenewEnabled != enabled)
        {
            var before = JsonSerializer.Serialize(new { p.PeriodAutoRenewEnabled });
            p.PeriodAutoRenewEnabled = enabled;
            db.ClientAuditEntries.Add(new() { ClientId = clientId, ActorUserId = current.UserId, Action = "PeriodRenewalChanged",
                BeforeJson = before, AfterJson = JsonSerializer.Serialize(new { p.PeriodAutoRenewEnabled }) });
            await db.SaveChangesAsync();
        }
        await tx.CommitAsync();
    }

    private async Task LockAsync(int clientId, int id)
    {
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock({74101}, {clientId})");
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock({74121}, {id})");
    }

    public async Task<int> ProcessDueAsync(CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var due = await db.ClientPackages.AsNoTracking().Where(p => p.BillingMode == PackageBillingMode.PeriodBased &&
            p.IsActive && p.ClosureDisposition == null && p.ValidUntil <= now).OrderBy(p => p.ValidUntil).ThenBy(p => p.Id)
            .Select(p => new { p.Id, p.ClientId }).Take(50).ToListAsync(ct);
        var count = 0;
        var paymentDueDays = due.Count == 0 ? 0 : (await settings.GetOwnerSettingsAsync()).DefaultPaymentDueDays;
        foreach (var item in due)
        {
            ct.ThrowIfCancellationRequested();
            await using var tx = await db.Database.BeginTransactionAsync(ct);
            await LockAsync(item.ClientId, item.Id);
            var p = await db.ClientPackages.IncludingOrganizationArchive(db.OrganizationId).Include(p => p.Client).Include(p => p.Package).SingleOrDefaultAsync(p => p.Id == item.Id, ct);
            if (p == null) { await tx.CommitAsync(ct); continue; }
            // Refresh after the lock, including when called repeatedly within one scope.
            await db.Entry(p).ReloadAsync(ct);
            if (!p.IsActive || p.ClosureDisposition != null || p.ValidUntil > now) { await tx.CommitAsync(ct); continue; }
            p.IsActive = false;
            var renew = p.PeriodAutoRenewEnabled && !p.Client.IsDeleted && p.Client.Status != "Blocked" &&
                p.Package != null && p.Package.IsActive && !p.Package.IsDeleted && p.Package.BillingMode == PackageBillingMode.PeriodBased;
            var nextEnd = AddDays(p.ValidUntil!.Value, p.PeriodDurationDays);
            renew = renew && !await db.ClientPackages.AnyAsync(n => n.PreviousClientPackageId == p.Id ||
                n.Id != p.Id && n.ClientId == p.ClientId && n.PackageId == p.PackageId && n.IsActive && n.ClosureDisposition == null &&
                n.BillingMode == PackageBillingMode.PeriodBased && n.PeriodStartsAt < nextEnd && n.ValidUntil > p.ValidUntil, ct);
            if (renew)
            {
                var start = p.ValidUntil!.Value;
                var next = new ClientPackage { ClientId = p.ClientId, PackageId = p.PackageId, Name = p.Name,
                    BillingMode = PackageBillingMode.PeriodBased, PeriodStartsAt = start, PeriodDurationDays = p.PeriodDurationDays,
                    ValidUntil = AddDays(start, p.PeriodDurationDays), PeriodAutoRenewEnabled = true,
                    TotalSessions = 0, SessionsPerWeek = 0, TotalPrice = p.TotalPrice, OriginalPrice = p.TotalPrice,
                    Currency = p.Currency, LocationId = p.LocationId, PurchaseDate = now, ActivatedAt = start,
                    PaymentStatus = p.TotalPrice == 0 ? PaymentStatus.Paid : PaymentStatus.Unpaid,
                    PaidAt = p.TotalPrice == 0 ? now : null, PaymentDueDate = p.TotalPrice == 0 ? null : AddDays(start, paymentDueDays),
                    PreviousClientPackageId = p.Id, IsActive = true, RenewalSource = "PeriodAutomatic" };
                db.ClientPackages.Add(next);
            }
            db.ClientAuditEntries.Add(new() { ClientId = p.ClientId, Action = renew ? "PeriodServiceRenewed" : "PeriodServiceExpired",
                BeforeJson = JsonSerializer.Serialize(new { clientPackageId = p.Id, isActive = true }),
                AfterJson = JsonSerializer.Serialize(new { clientPackageId = p.Id, isActive = false, renewed = renew }) });
            await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); count++;
        }
        return count;
    }
}
