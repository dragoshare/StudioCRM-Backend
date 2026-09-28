using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using StudioCRM.Application.DTOs.Billing;
using StudioCRM.Application.DTOs.Clients;
using StudioCRM.Domain.Entities;

namespace StudioCRM.Infrastructure.Services;

public partial class ClientService
{
    private static string ClientAuditState(Client client) => JsonSerializer.Serialize(new
    {
        client.FirstName, client.LastName, client.Email, client.PhoneNumber,
        client.LocationId, client.TrainerId, client.Status, client.IsDeleted, client.PortalAccessBlocked,
        client.SubscriptionAutoRenewEnabled, client.ActivePackageId, client.NextPackageId
    });

    private void AuditClient(Client client, string action, string before, string? reason = null)
    {
        var after = ClientAuditState(client);
        if (before == after) return;
        _context.ClientAuditEntries.Add(new ClientAuditEntry
        {
            Client = client, ActorUserId = _currentUser.UserId, Action = action,
            BeforeJson = before, AfterJson = after, Reason = reason
        });
    }

    private async Task ValidateTrainerLocationAsync(int? trainerId, int locationId)
    {
        if (trainerId.HasValue && !await _context.TrainerLocations.AnyAsync(x =>
                x.TrainerId == trainerId && x.LocationId == locationId && x.Trainer.User.IsActive))
            throw new InvalidOperationException("Trainer does not work at the client's location.");
    }

    public async Task<PagedResultDto<ClientAuditDto>> GetAuditAsync(int clientId, int page = 1, int pageSize = 25)
    {
        EnsureOwner();
        await EnsureReadableClientAsync(clientId);
        ValidatePage(page, pageSize);
        var query = _context.ClientAuditEntries.Where(x => x.ClientId == clientId);
        var total = await query.CountAsync();
        var items = await query.OrderByDescending(x => x.Id).Skip((page - 1) * pageSize).Take(pageSize)
            .Select(x => new ClientAuditDto { Id = x.Id, Action = x.Action, ActorUserId = x.ActorUserId,
                BeforeJson = x.BeforeJson, AfterJson = x.AfterJson, Reason = x.Reason, CreatedAt = x.CreatedAt }).ToListAsync();
        return Page(items, total, page, pageSize);
    }

    public async Task<PagedResultDto<ClientSessionHistoryDto>> GetSessionHistoryAsync(int clientId, ClientHistoryFilter request)
    {
        await EnsureReadableClientAsync(clientId);
        ValidatePage(request.Page, request.PageSize);
        var query = _context.SessionParticipants.IgnoreQueryFilters()
            .Where(x => x.ClientId == clientId && !x.Session.IsDeleted);
        var now = DateTime.UtcNow;
        query = request.Scope switch
        {
            "Upcoming" => query.Where(x => x.Session.StartAt >= now && x.Session.Status != "Cancelled"),
            "Past" => query.Where(x => x.Session.StartAt < now && x.Session.Status != "Cancelled"),
            "Cancelled" => query.Where(x => x.Session.Status == "Cancelled"),
            "All" => query,
            _ => throw new InvalidOperationException("Scope must be All, Upcoming, Past or Cancelled.")
        };
        var total = await query.CountAsync();
        var ordered = request.Scope == "Upcoming" ? query.OrderBy(x => x.Session.StartAt).ThenBy(x => x.Id)
            : query.OrderByDescending(x => x.Session.StartAt).ThenByDescending(x => x.Id);
        var items = await ordered.Skip((request.Page - 1) * request.PageSize).Take(request.PageSize)
            .Select(x => new ClientSessionHistoryDto
            {
                SessionId = x.SessionId, Title = x.Session.Title, StartAt = x.Session.StartAt,
                EndAt = x.Session.EndAt, Status = x.Session.Status,
                TrainerId = x.Session.TrainerId, TrainerName = x.Session.Trainer.User.FirstName + " " + x.Session.Trainer.User.LastName,
                LocationId = x.Session.LocationId, LocationName = x.Session.Location.Name,
                AttendanceStatus = x.AttendanceStatus, IsCountedFromPackage = x.IsCountedFromPackage,
                ClientPackageId = x.ClientPackageId
            }).ToListAsync();
        return Page(items, total, request.Page, request.PageSize);
    }

    private async Task EnsureReadableClientAsync(int clientId)
    {
        if (!await ApplyAccessControl(BuildClientQuery(includeArchived: true)).AnyAsync(x => x.Id == clientId))
            throw new KeyNotFoundException("Client not found.");
    }

    private static void ValidatePage(int page, int pageSize)
    {
        if (page < 1 || page > 100000 || pageSize < 1 || pageSize > 100)
            throw new InvalidOperationException("Page must be 1..100000 and pageSize 1..100.");
    }

    private static PagedResultDto<T> Page<T>(List<T> items, int total, int page, int pageSize) => new()
    {
        Items = items, TotalCount = total, Page = page, PageSize = pageSize,
        TotalPages = (int)Math.Ceiling(total / (double)pageSize)
    };
}
