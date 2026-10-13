using Microsoft.EntityFrameworkCore;
using StudioCRM.Application.DTOs.SessionParticipants;
using StudioCRM.Domain.Enums;

namespace StudioCRM.Infrastructure.Services;

public partial class GroupEnrollmentService
{
    public async Task<GroupPackageOptionsDto> GetPackageOptionsAsync(int sessionId, int clientId)
    {
        if (!current.IsOwner) throw new UnauthorizedAccessException("Owner required.");
        var session = await db.Sessions.AsNoTracking().SingleOrDefaultAsync(s => s.Id == sessionId)
            ?? throw new KeyNotFoundException("Session not found.");
        if (!GroupBookingPolicy.IsGroup(session) || session.Status != "Planned" || session.StartAt <= DateTime.UtcNow)
            throw new InvalidOperationException("Choose a future planned group class.");
        var client = await db.Clients.AsNoTracking().SingleOrDefaultAsync(c => c.Id == clientId)
            ?? throw new KeyNotFoundException("Client not found.");
        if (client.IsDeleted || client.Status == "Blocked") throw new InvalidOperationException("Client is unavailable.");
        if (!await db.ClientLocationMemberships.AnyAsync(m => m.ClientId == clientId && m.LocationId == session.LocationId && m.GroupAccessEnabled))
            throw new InvalidOperationException("Enable group access for the client at this location first.");

        var now = DateTime.UtcNow;
        var catalog = await db.Packages.AsNoTracking().Where(p => p.IsActive && p.BillingType == SessionBillingType.Group &&
            (p.LocationId == null || p.LocationId == session.LocationId) && p.SessionsLimit > 0 && p.Price >= 0 && p.DurationDays > 0)
            .OrderBy(p => p.Name).ThenBy(p => p.Id)
            .Select(p => new { p.Id, p.Name, p.Price, p.Currency, p.SessionsLimit, p.DurationDays }).ToListAsync();
        var offers = catalog.Select(p => new GroupPackageOfferDto(p.Id, p.Name, p.Price, p.Currency, p.SessionsLimit, p.DurationDays,
            (session.StartAt - (p.Price == 0 ? now : now.Date)).TotalDays <= p.DurationDays,
            (session.StartAt - (p.Price == 0 ? now : now.Date)).TotalDays <= p.DurationDays ? [] : ["ValidityTooShortFromToday"])).ToList();
        var owned = await db.ClientPackages.AsNoTracking().Where(p => p.ClientId == clientId && p.ExpectedBillingType == SessionBillingType.Group)
            .OrderBy(p => p.ValidUntil).ThenBy(p => p.Id)
            .Select(p => new { Package = p, Reserved = db.SessionParticipants.Where(x => x.ClientPackageId == p.Id &&
                !x.IsCountedFromPackage && x.CountsAgainstPackage && x.SessionId != sessionId &&
                x.Session.Status != "Cancelled" && x.Session.Status != "Completed" &&
                x.AttendanceStatus != "CancelledInTime" && x.AttendanceStatus != "CancelledLate").Sum(x => x.SessionsCharged) }).ToListAsync();
        var packages = new List<OwnedGroupPackageDto>();
        foreach (var row in owned)
        {
            var p = row.Package;
            var reasons = new List<string>();
            if (p.LocationId.HasValue && p.LocationId != session.LocationId) reasons.Add("LocationMismatch");
            if (p.ClosureDisposition != null) reasons.Add("Closed");
            if (p.ValidUntil < session.StartAt) reasons.Add("ExpiredOnSessionDate");
            var available = Math.Max(0, p.TotalSessions - p.UsedSessions - row.Reserved);
            if (available < 1) reasons.Add("NoAvailableEntries");
            var payable = reasons.Count == 0 && p.PaymentStatus != PaymentStatus.Paid;
            if (!p.IsActive) reasons.Add("Inactive");
            if (p.PaymentStatus != PaymentStatus.Paid) reasons.Add("PaymentRequired");
            var eligible = reasons.Count == 0;
            var offer = offers.SingleOrDefault(o => o.PackageId == p.PackageId);
            var canSelect = offer != null && (eligible || payable && (p.ValidUntil.HasValue || offer.CanSelect));
            if (offer == null) reasons.Add("OfferUnavailable");
            if (payable && !p.ValidUntil.HasValue && offer != null && !offer.CanSelect) reasons.Add("ValidityTooShortFromToday");
            packages.Add(new(p.Id, p.PackageId, p.Name, p.TotalPrice, p.Currency, Math.Max(0, p.TotalPrice - p.AmountPaid),
                p.LocationId, p.ValidUntil, p.PaymentStatus.ToString(), p.IsActive, p.TotalSessions, p.UsedSessions,
                row.Reserved, available, eligible, canSelect, reasons));
        }
        return new(session.Id, client.Id, session.LocationId, session.StartAt, offers, packages);
    }
}
