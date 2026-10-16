using Microsoft.EntityFrameworkCore;
using StudioCRM.Application.DTOs.SessionParticipants;
using StudioCRM.Application.Interfaces;
using StudioCRM.Domain.Entities;
using StudioCRM.Domain.Enums;
using StudioCRM.Infrastructure.Persistence;
using StudioCRM.Application.Common;
namespace StudioCRM.Infrastructure.Services;

public partial class GroupEnrollmentService(StudioCRMDbContext db, ICurrentUserService current, ISessionParticipantService participants)
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
            if (previous.PackageId != request.PackageId || request.ClientPackageId.HasValue && previous.Id != request.ClientPackageId.Value) throw new DbUpdateConcurrencyException("A different package was already selected for this class.");
            await tx.CommitAsync(); return await GetAsync(existing.Id);
        }
        var catalog = await db.Packages.SingleOrDefaultAsync(p => p.Id == request.PackageId && p.IsActive && p.BillingType == SessionBillingType.Group &&
            (p.LocationId == null || p.LocationId == session.LocationId)) ?? throw new InvalidOperationException("Valid group package required.");
        if (catalog.SessionsLimit <= 0 || catalog.Price < 0 || catalog.DurationDays <= 0) throw new InvalidOperationException("Invalid group package configuration.");
        if (!await db.ClientLocationMemberships.AnyAsync(m => m.ClientId == client.Id && m.LocationId == session.LocationId && m.GroupAccessEnabled))
            throw new InvalidOperationException("Enable group access for the client at this location first.");
        var options = await GetPackageOptionsAsync(sessionId, client.Id);
        ClientPackage? package = null;
        if (request.ClientPackageId.HasValue)
        {
            var choice = options.OwnedPackages.SingleOrDefault(p => p.ClientPackageId == request.ClientPackageId && p.PackageId == catalog.Id);
            if (choice == null || !choice.CanSelect) throw new InvalidOperationException("Selected client package is unavailable.");
            package = await db.ClientPackages.SingleAsync(p => p.Id == choice.ClientPackageId);
        }
        else
        {
            var choice = options.OwnedPackages.Where(p => p.PackageId == catalog.Id && p.CanSelect)
                .OrderByDescending(p => p.IsEligible).ThenBy(p => p.ValidUntil ?? DateTime.MaxValue).ThenBy(p => p.ClientPackageId).FirstOrDefault();
            if (choice != null) package = await db.ClientPackages.SingleAsync(p => p.Id == choice.ClientPackageId);
        }
        if (package == null && !options.Offers.Any(p => p.PackageId == catalog.Id && p.CanSelect))
            throw new InvalidOperationException("The offer cannot cover the session date.");
        if (package == null)
        {
            var now = DateTime.UtcNow; var free = catalog.Price == 0;
            package = new ClientPackage { ClientId = client.Id, PackageId = catalog.Id, Name = catalog.Name,
                LocationId = session.LocationId, TotalSessions = catalog.SessionsLimit, SessionsPerWeek = catalog.SessionsPerWeek,
                TotalPrice = catalog.Price, OriginalPrice = catalog.Price, ExpectedUnitPrice = decimal.Round(catalog.Price / catalog.SessionsLimit, 2),
                Currency = catalog.Currency, ExpectedBillingType = SessionBillingType.Group, PurchaseDate = now,
                PaymentStatus = free ? PaymentStatus.Paid : PaymentStatus.Unpaid, IsActive = free,
                ValidUntil = free ? PackageValidity.EndOfStudioDay(now.AddDays(catalog.DurationDays)) : null, PaidAt = free ? now : null,
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
        var items = await ReadAsync(query.OrderBy(e => e.Id).Skip((page - 1) * pageSize).Take(pageSize));
        return new() { Page = page, PageSize = pageSize, TotalCount = count, TotalPages = (int)Math.Ceiling(count / (double)pageSize), Items = items };
    }

    // One read model for list, details and prepare/confirm responses: a fixed number of queries per page (no per-row reads).
    private async Task<List<GroupEnrollmentDto>> ReadAsync(IQueryable<GroupEnrollmentRequest> query)
    {
        var rows = await query
            .Select(e => new { e.Id, e.SessionId, e.ClientId, e.ClientPackageId, e.Session.Status, e.Session.StartAt,
                e.ClientPackage.ClosureDisposition, e.ClientPackage.ValidUntil, e.ClientPackage.PaymentStatus,
                Due = Math.Max(0, e.ClientPackage.TotalPrice - e.ClientPackage.AmountPaid),
                Booked = db.SessionParticipants.Any(p => p.SessionId == e.SessionId && p.ClientId == e.ClientId && p.AttendanceStatus != "CancelledInTime" && p.AttendanceStatus != "CancelledLate"),
                PackageName = e.ClientPackage.Name, e.ClientPackage.TotalPrice, e.ClientPackage.AmountPaid, PackageActive = e.ClientPackage.IsActive,
                e.ClientPackage.TotalSessions, e.ClientPackage.UsedSessions, e.ClientPackage.Currency, e.ClientPackage.BillingMode,
                Participant = db.SessionParticipants.Where(p => p.SessionId == e.SessionId && p.ClientId == e.ClientId)
                    .OrderByDescending(p => p.Id).Select(p => new { p.Id, p.AttendanceStatus }).FirstOrDefault() }).ToListAsync();
        // Separate read so archived clients keep their rows (Client has a soft-delete query filter).
        var clientIds = rows.Select(r => r.ClientId).Distinct().ToArray();
        var clients = await db.Clients.IncludingOrganizationArchive(db.OrganizationId).AsNoTracking().Where(c => clientIds.Contains(c.Id))
            .Select(c => new { c.Id, Name = c.FirstName + " " + c.LastName, c.Email, c.PhoneNumber, c.IsDeleted }).ToDictionaryAsync(c => c.Id);
        var packageIds = rows.Select(r => (int?)r.ClientPackageId).Distinct().ToArray();
        var pending = (await db.ClientPayments.AsNoTracking()
            .Where(p => packageIds.Contains(p.ClientPackageId) && p.Status == ClientPaymentStatus.PendingConfirmation)
            .OrderBy(p => p.PaymentDate).ThenBy(p => p.Id)
            .Select(p => new { p.ClientPackageId, p.Id, p.Amount, p.Currency, p.Method, p.Source, p.PaymentDate })
            .ToListAsync()).ToLookup(p => p.ClientPackageId!.Value,
                p => new GroupEnrollmentPendingPaymentDto(p.Id, p.Amount, p.Currency, p.Method, p.Source, p.PaymentDate));
        return rows.Select(e =>
        {
            var status = e.Booked ? "Booked" : e.Status != "Planned" || e.StartAt <= DateTime.UtcNow ? "Unavailable" :
                e.ClosureDisposition != null || e.ValidUntil < e.StartAt ? "PackageUnavailable" :
                e.PaymentStatus != PaymentStatus.Paid ? "AwaitingPayment" : "ReadyToBook";
            var client = clients.GetValueOrDefault(e.ClientId);
            var pendingPayments = pending[e.ClientPackageId].ToList();
            return new GroupEnrollmentDto(e.Id, e.SessionId, e.ClientId, e.ClientPackageId, status, e.Due, e.Booked)
            {
                ClientName = client?.Name, ClientEmail = client?.Email, ClientPhone = client?.PhoneNumber, ClientIsArchived = client?.IsDeleted,
                PackageName = e.PackageName, PackageTotalPrice = e.TotalPrice, PackageAmountPaid = e.AmountPaid,
                PackagePaymentStatus = e.PaymentStatus.ToString(), PackageValidUntil = e.ValidUntil, PackageIsActive = e.PackageActive,
                PackageTotalEntries = e.TotalSessions, PackageUsedEntries = e.UsedSessions,
                PackageBillingMode = e.BillingMode, Currency = e.Currency,
                PendingPayments = pendingPayments, PendingPaymentsAmount = pendingPayments.Sum(p => p.Amount),
                SessionParticipantId = e.Participant?.Id, AttendanceStatus = e.Participant?.AttendanceStatus,
                AllowedActions = current.IsOwner ? AllowedActions(status, e.Due, client?.IsDeleted ?? true) : []
            };
        }).ToList();
    }

    // Mirrors ConfirmAsync (only ReadyToBook can be confirmed); a payment is recorded through the billing endpoint.
    private static List<string> AllowedActions(string status, decimal due, bool clientArchived) => clientArchived ? [] : status switch
    {
        "ReadyToBook" => ["Confirm"],
        "AwaitingPayment" when due > 0 => ["RecordPayment"],
        _ => []
    };

    private async Task<GroupEnrollmentRequest> AccessibleAsync(int id)
    {
        return await db.GroupEnrollmentRequests.AsNoTracking().Include(x => x.ClientPackage).Include(x => x.Session)
            .Where(x => x.Id == id && (current.IsOwner || x.Client.UserId == current.UserId && current.IsClient))
            .SingleOrDefaultAsync() ?? throw new KeyNotFoundException("Enrollment not found.");
    }

    public async Task<GroupEnrollmentDto> GetAsync(int id)
    {
        var e = await AccessibleAsync(id);
        return (await ReadAsync(db.GroupEnrollmentRequests.AsNoTracking().Where(x => x.Id == e.Id))).Single();
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
