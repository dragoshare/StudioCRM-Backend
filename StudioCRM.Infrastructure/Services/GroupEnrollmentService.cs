using Microsoft.EntityFrameworkCore;
using StudioCRM.Application.DTOs.SessionParticipants;
using StudioCRM.Application.Interfaces;
using StudioCRM.Domain.Entities;
using StudioCRM.Domain.Enums;
using StudioCRM.Infrastructure.Persistence;
namespace StudioCRM.Infrastructure.Services;

public class GroupEnrollmentService(StudioCRMDbContext db, ICurrentUserService current, ISessionParticipantService participants)
{
    public async Task<GroupEnrollmentDto> PrepareAsync(int sessionId, PrepareGroupEnrollmentRequest request)
    {
        if (!current.IsOwner) throw new UnauthorizedAccessException("Owner required.");
        await using var tx = await db.Database.BeginTransactionAsync();
        await SessionWriteLock.AcquireAsync(db, sessionId, [request.ClientId]);
        var session = await db.Sessions.SingleOrDefaultAsync(s => s.Id == sessionId) ?? throw new KeyNotFoundException("Session not found.");
        if (!GroupBookingPolicy.IsGroup(session) || session.Status != "Planned" || session.StartAt <= DateTime.UtcNow)
            throw new InvalidOperationException("Choose a future planned group class.");
        var client = await db.Clients.SingleOrDefaultAsync(c => c.Id == request.ClientId) ?? throw new KeyNotFoundException("Client not found.");
        if (client.Status == "Blocked" || client.IsDeleted) throw new InvalidOperationException("Client is unavailable.");
        var existing = await db.GroupEnrollmentRequests.SingleOrDefaultAsync(x => x.SessionId == sessionId && x.ClientId == client.Id);
        if (existing != null)
        {
            var previous = await db.ClientPackages.SingleAsync(p => p.Id == existing.ClientPackageId);
            if (previous.PackageId != request.PackageId) throw new DbUpdateConcurrencyException("A different package was already selected for this class.");
            await tx.CommitAsync(); return await GetAsync(existing.Id);
        }
        var catalog = await db.Packages.SingleOrDefaultAsync(p => p.Id == request.PackageId && p.IsActive && p.BillingType == SessionBillingType.Group &&
            (p.LocationId == null || p.LocationId == session.LocationId)) ?? throw new InvalidOperationException("Valid group package required.");
        if (catalog.SessionsLimit <= 0 || catalog.Price < 0 || catalog.DurationDays <= 0) throw new InvalidOperationException("Invalid group package configuration.");
        if (!await db.ClientLocationMemberships.AnyAsync(m => m.ClientId == client.Id && m.LocationId == session.LocationId && m.GroupAccessEnabled))
            throw new InvalidOperationException("Enable group access for the client at this location first.");
        var package = await db.ClientPackages.Where(p => p.ClientId == client.Id && p.PackageId == catalog.Id && p.ExpectedBillingType == SessionBillingType.Group && p.ClosureDisposition == null &&
            p.LocationId == session.LocationId && (p.PaymentStatus != PaymentStatus.Paid || p.IsActive && (p.ValidUntil == null || p.ValidUntil >= session.StartAt) && p.UsedSessions < p.TotalSessions))
            .OrderByDescending(p => p.PurchaseDate).FirstOrDefaultAsync();
        if (package == null)
        {
            var now = DateTime.UtcNow; var free = catalog.Price == 0;
            package = new ClientPackage { ClientId = client.Id, PackageId = catalog.Id, Name = catalog.Name,
                LocationId = session.LocationId, TotalSessions = catalog.SessionsLimit, SessionsPerWeek = catalog.SessionsPerWeek,
                TotalPrice = catalog.Price, OriginalPrice = catalog.Price, ExpectedUnitPrice = decimal.Round(catalog.Price / catalog.SessionsLimit, 2),
                Currency = catalog.Currency, ExpectedBillingType = SessionBillingType.Group, PurchaseDate = now,
                PaymentStatus = free ? PaymentStatus.Paid : PaymentStatus.Unpaid, IsActive = free,
                ValidUntil = free ? now.AddDays(catalog.DurationDays) : null, PaidAt = free ? now : null,
                ActivatedAt = free ? now : null, PaymentDueDate = free ? null : now.AddDays(3),
                RequestedByUserId = current.UserId, RenewalSource = "GroupOwnerEnrollment" };
            db.ClientPackages.Add(package); await db.SaveChangesAsync();
        }
        var enrollment = new GroupEnrollmentRequest { SessionId = sessionId, ClientId = client.Id, ClientPackageId = package.Id, CreatedByUserId = current.UserId };
        db.GroupEnrollmentRequests.Add(enrollment); await db.SaveChangesAsync();
        if (client.UserId.HasValue && package.PaymentStatus != PaymentStatus.Paid)
            await NotificationWriter.QueueAsync(db, new[] { client.UserId.Value }, $"group-enrollment:{enrollment.Id}:payment", "GroupPackagePaymentRequired",
                "Opłać pakiet zajęć grupowych", "Pakiet oczekuje na płatność. Miejsce na zajęciach zostanie sprawdzone przy zapisie.",
                relatedEntityType: "client_package", relatedEntityId: package.Id, actionUrl: "/client/payments");
        await db.SaveChangesAsync(); await tx.CommitAsync(); return await GetAsync(enrollment.Id);
    }

    public async Task<StudioCRM.Application.DTOs.Billing.PagedResultDto<GroupEnrollmentDto>> ListAsync(int sessionId, int page, int pageSize)
    {
        if (!current.IsOwner) throw new UnauthorizedAccessException("Owner required.");
        if (page < 1 || page > 100000 || pageSize < 1 || pageSize > 100) throw new InvalidOperationException("Invalid pagination.");
        var query = db.GroupEnrollmentRequests.AsNoTracking().Where(e => e.SessionId == sessionId);
        var count = await query.CountAsync();
        var rows = await query.OrderBy(e => e.Id).Skip((page - 1) * pageSize).Take(pageSize)
            .Select(e => new { e.Id, e.SessionId, e.ClientId, e.ClientPackageId, e.Session.Status, e.Session.StartAt,
                e.ClientPackage.ClosureDisposition, e.ClientPackage.ValidUntil, e.ClientPackage.PaymentStatus,
                Due = Math.Max(0, e.ClientPackage.TotalPrice - e.ClientPackage.AmountPaid),
                Booked = db.SessionParticipants.Any(p => p.SessionId == e.SessionId && p.ClientId == e.ClientId && p.AttendanceStatus != "CancelledInTime" && p.AttendanceStatus != "CancelledLate") }).ToListAsync();
        return new() { Page = page, PageSize = pageSize, TotalCount = count, TotalPages = (int)Math.Ceiling(count / (double)pageSize),
            Items = rows.Select(e => new GroupEnrollmentDto(e.Id, e.SessionId, e.ClientId, e.ClientPackageId,
                e.Booked ? "Booked" : e.Status != "Planned" || e.StartAt <= DateTime.UtcNow ? "Unavailable" :
                e.ClosureDisposition != null || e.ValidUntil < e.StartAt ? "PackageUnavailable" :
                e.PaymentStatus != PaymentStatus.Paid ? "AwaitingPayment" : "ReadyToBook", e.Due, e.Booked)).ToList() };
    }

    private async Task<GroupEnrollmentRequest> AccessibleAsync(int id)
    {
        return await db.GroupEnrollmentRequests.AsNoTracking().Include(x => x.ClientPackage).Include(x => x.Session)
            .Where(x => x.Id == id && (current.IsOwner || x.Client.UserId == current.UserId && current.IsClient))
            .SingleOrDefaultAsync() ?? throw new KeyNotFoundException("Enrollment not found.");
    }

    public async Task<GroupEnrollmentDto> GetAsync(int id)
    {
        var e = await AccessibleAsync(id); var p = e.ClientPackage;
        var booked = await db.SessionParticipants.AnyAsync(x => x.SessionId == e.SessionId && x.ClientId == e.ClientId &&
            x.AttendanceStatus != "CancelledInTime" && x.AttendanceStatus != "CancelledLate");
        var status = booked ? "Booked" : e.Session.Status != "Planned" || e.Session.StartAt <= DateTime.UtcNow ? "Unavailable" :
            p.ClosureDisposition != null || p.ValidUntil < e.Session.StartAt ? "PackageUnavailable" :
            p.PaymentStatus != PaymentStatus.Paid ? "AwaitingPayment" : "ReadyToBook";
        return new(e.Id, e.SessionId, e.ClientId, p.Id, status, Math.Max(0, p.TotalPrice - p.AmountPaid), booked);
    }

    public async Task<GroupEnrollmentDto> ConfirmAsync(int id)
    {
        var e = await AccessibleAsync(id);
        var state = await GetAsync(id);
        if (state.Status == "Booked") return state;
        if (state.Status != "ReadyToBook") throw new InvalidOperationException("Enrollment is not ready; payment and package validity must be confirmed first.");
        // AddParticipant owns the reservation transaction and rechecks package, seats and authorization.
        if (!current.IsOwner) throw new UnauthorizedAccessException("Owner must confirm this enrollment; clients can use normal public booking.");
        await participants.AddParticipantAsync(e.SessionId, new AddSessionParticipantDto { ClientId = e.ClientId, ClientPackageId = e.ClientPackageId });
        return await GetAsync(id);
    }
}
