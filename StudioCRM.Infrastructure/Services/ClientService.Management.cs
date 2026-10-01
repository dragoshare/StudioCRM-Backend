using System.Data;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using StudioCRM.Application.DTOs.Billing;
using StudioCRM.Application.DTOs.Clients;
using StudioCRM.Domain.Entities;

namespace StudioCRM.Infrastructure.Services;

public partial class ClientService
{
    public async Task<List<ClientLocationDto>> GetLocationsAsync(int clientId)
    {
        await EnsureReadableClientAsync(clientId);
        return await _context.ClientLocationMemberships.IgnoreQueryFilters().Where(x => x.ClientId == clientId)
            .OrderByDescending(x => x.IsHomeLocation).ThenBy(x => x.LocationId)
            .Select(x => new ClientLocationDto { LocationId = x.LocationId, Name = x.Location.Name,
                IsHomeLocation = x.IsHomeLocation, GroupAccessEnabled = x.GroupAccessEnabled }).ToListAsync();
    }

    public async Task SetLocationAccessAsync(int clientId, int locationId, SetClientLocationAccessRequest request)
    {
        EnsureOwner();
        ValidateReason(request.Reason);
        await using var tx = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        var client = await _context.Clients.SingleOrDefaultAsync(x => x.Id == clientId)
            ?? throw new KeyNotFoundException("Client not found or archived.");
        if (!await _context.Locations.AnyAsync(x => x.Id == locationId)) throw new KeyNotFoundException("Location not found.");
        var membership = await _context.ClientLocationMemberships.SingleOrDefaultAsync(x => x.ClientId == clientId && x.LocationId == locationId);
        var before = JsonSerializer.Serialize(new { LocationId = locationId, Exists = membership != null, GroupAccessEnabled = membership?.GroupAccessEnabled });
        if (membership == null)
        {
            membership = new ClientLocationMembership { ClientId = clientId, LocationId = locationId, IsHomeLocation = client.LocationId == locationId };
            _context.ClientLocationMemberships.Add(membership);
        }
        membership.GroupAccessEnabled = request.GroupAccessEnabled;
        membership.UpdatedAt = DateTime.UtcNow;
        _context.ClientAuditEntries.Add(new ClientAuditEntry { ClientId = clientId, ActorUserId = _currentUser.UserId,
            Action = "LocationAccessChanged", Reason = request.Reason.Trim(), BeforeJson = before,
            AfterJson = JsonSerializer.Serialize(new { LocationId = locationId, Exists = true, request.GroupAccessEnabled }) });
        await _context.SaveChangesAsync();
        await tx.CommitAsync();
    }

    public async Task RemoveLocationAsync(int clientId, int locationId)
    {
        EnsureOwner();
        await using var tx = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        var client = await _context.Clients.SingleOrDefaultAsync(x => x.Id == clientId)
            ?? throw new KeyNotFoundException("Client not found or archived.");
        if (client.LocationId == locationId) throw new InvalidOperationException("Change the home location before removing this membership.");
        if (await _context.SessionParticipants.AnyAsync(x => x.ClientId == clientId && x.Session.LocationId == locationId &&
                x.Session.Status != "Cancelled" && (x.Session.EndAt > DateTime.UtcNow || x.Session.Status != "Completed")) ||
            await _context.ClientPackages.AnyAsync(x => x.ClientId == clientId && x.LocationId == locationId && x.IsActive))
            throw new InvalidOperationException("Resolve active packages and unfinished or future sessions in this location first.");
        var membership = await _context.ClientLocationMemberships.SingleOrDefaultAsync(x => x.ClientId == clientId && x.LocationId == locationId);
        if (membership == null) return;
        _context.ClientLocationMemberships.Remove(membership);
        _context.ClientAuditEntries.Add(new ClientAuditEntry { ClientId = clientId, ActorUserId = _currentUser.UserId,
            Action = "LocationMembershipRemoved", BeforeJson = JsonSerializer.Serialize(new { locationId, membership.GroupAccessEnabled }) });
        await _context.SaveChangesAsync();
        await tx.CommitAsync();
    }

    public async Task<List<ClientDuplicateDto>> FindDuplicatesAsync(ClientDuplicateFilter filter)
    {
        EnsureOwner();
        var email = NormalizeContactEmail(filter.Email);
        var phone = NormalizeDuplicatePhone(filter.Phone);
        var name = filter.Name?.Trim().ToLowerInvariant() ?? "";
        if (email.Length > 320 || phone.Length > 30 || name.Length > 300) throw new InvalidOperationException("Search input is too long.");
        if (email.Length == 0 && phone.Length < 6 && name.Length < 3) throw new InvalidOperationException("Provide an email, phone of at least 6 digits or name of at least 3 characters.");
        // Exact normalized matches warn without blocking shared family contact details.
        var query = _context.Clients.IgnoreQueryFilters().Where(x => x.Id != filter.ExcludeClientId &&
            ((email != "" && x.Email.ToLower() == email) ||
             (phone.Length >= 6 && (x.PhoneNumber ?? "").Replace(" ", "").Replace("+", "").Replace("-", "").Replace("(", "").Replace(")", "").Replace(".", "") == phone) ||
             (name.Length >= 3 && (x.FirstName + " " + x.LastName).ToLower() == name)));
        var matches = await query.OrderBy(x => x.Id).Take(50).ToListAsync();
        return matches.Select(x => new ClientDuplicateDto { ClientId = x.Id, FullName = (x.FirstName + " " + x.LastName).Trim(), IsArchived = x.IsDeleted,
            Matches = new[] { email != "" && NormalizeContactEmail(x.Email) == email ? "Email" : null,
                phone.Length >= 6 && NormalizeDuplicatePhone(x.PhoneNumber) == phone ? "Phone" : null,
                name.Length >= 3 && (x.FirstName + " " + x.LastName).Trim().ToLowerInvariant() == name ? "Name" : null }
                .Where(x => x != null).Select(x => x!).ToList() }).ToList();
    }
    private static string NormalizeDuplicatePhone(string? value) => (value ?? "").Replace(" ", "").Replace("+", "").Replace("-", "").Replace("(", "").Replace(")", "").Replace(".", "");

    public async Task<PagedResultDto<ClientPackageBillingDto>> GetPackageHistoryAsync(int clientId, int page = 1, int pageSize = 25)
    {
        await EnsureReadableClientAsync(clientId);
        ValidatePage(page, pageSize);
        var query = _context.ClientPackages.IgnoreQueryFilters().Include(x => x.Location).Include(x => x.Client).Where(x => x.ClientId == clientId);
        var total = await query.CountAsync();
        var packages = await query.OrderByDescending(x => x.PurchaseDate).ThenByDescending(x => x.Id).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();
        var nextSessions = await ClientPackageSchedule.GetNextSessionsAsync(_context, clientId, packages.Select(x => x.Id));
        return Page(packages.Select(x => ClientPaymentService.MapPackage(x, nextSessionAt: nextSessions.TryGetValue(x.Id, out var next) ? next : null)).ToList(), total, page, pageSize);
    }

    public async Task<PagedResultDto<ClientRefundDto>> GetRefundsAsync(int? clientId, int page = 1, int pageSize = 25)
    {
        EnsureOwner();
        ValidatePage(page, pageSize);
        if (clientId.HasValue) await EnsureReadableClientAsync(clientId.Value);
        var query = _context.ClientPackages.IgnoreQueryFilters().Where(x => x.ClosureDisposition == "RefundPending" || x.ClosureDisposition == "Refunded");
        if (clientId.HasValue) query = query.Where(x => x.ClientId == clientId);
        var total = await query.CountAsync();
        var items = await query.OrderByDescending(x => x.ClosedAt).ThenByDescending(x => x.Id).Skip((page - 1) * pageSize).Take(pageSize)
            .Select(x => new ClientRefundDto { ClientId = x.ClientId, ClientPackageId = x.Id, PackageName = x.Name,
                Disposition = x.ClosureDisposition!, Amount = x.RefundAmount, Currency = x.Currency,
                ConfirmedAt = x.RefundConfirmedAt, Reference = x.RefundReference }).ToListAsync();
        return Page(items, total, page, pageSize);
    }
}
