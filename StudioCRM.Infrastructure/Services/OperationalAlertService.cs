using Microsoft.EntityFrameworkCore;
using StudioCRM.Application.DTOs.Alerts;
using StudioCRM.Application.Interfaces;
using StudioCRM.Domain.Entities;
using StudioCRM.Domain.Enums;
using StudioCRM.Infrastructure.Persistence;

namespace StudioCRM.Infrastructure.Services;

public class OperationalAlertService : IOperationalAlertService
{
    private const int LocationPeopleLimit = 8;

    private readonly StudioCRMDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public OperationalAlertService(
        StudioCRMDbContext context,
        ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<OperationalAlertsDto> GetAlertsAsync(OperationalAlertFilterDto filter)
    {
        var limit = filter.Limit is <= 0 or > 200 ? 50 : filter.Limit;
        var alerts = new List<OperationalAlertDto>();
        var trainerId = await GetCurrentTrainerIdAsync();

        if (!_currentUser.IsOwner && trainerId is null)
            return new OperationalAlertsDto();

        alerts.AddRange(await BuildPendingPaymentAlertsAsync(filter, trainerId));
        alerts.AddRange(await BuildClientWithoutPackageAlertsAsync(filter, trainerId));
        alerts.AddRange(await BuildPackageEndingAlertsAsync(filter, trainerId));
        alerts.AddRange(await BuildRenewalCancellationAlertsAsync(filter, trainerId));
        alerts.AddRange(await BuildOutlookSyncAlertsAsync(filter, trainerId));
        alerts.AddRange(await BuildLocationLimitAlertsAsync(filter, trainerId));
        alerts.AddRange(await BuildInvitationAlertsAsync(filter, trainerId));

        if (!string.IsNullOrWhiteSpace(filter.Type))
            alerts = alerts
                .Where(a => a.Type.Equals(filter.Type, StringComparison.OrdinalIgnoreCase))
                .ToList();

        return new OperationalAlertsDto
        {
            Items = alerts
                .OrderByDescending(a => SeverityRank(a.Severity))
                .ThenBy(a => a.DueAt ?? a.CreatedAt)
                .Take(limit)
                .ToList()
        };
    }

    private async Task<List<OperationalAlertDto>> BuildPendingPaymentAlertsAsync(
        OperationalAlertFilterDto filter,
        int? trainerId)
    {
        var query = _context.ClientPayments
            .Include(p => p.Client)
                .ThenInclude(c => c.Location)
            .Where(p => p.Status == ClientPaymentStatus.PendingConfirmation);

        query = ApplyClientScope(query, filter.LocationId, trainerId);

        return await query
            .OrderBy(p => p.CreatedAt)
            .Take(20)
            .Select(p => new OperationalAlertDto
            {
                Type = "PaymentPendingConfirmation",
                Severity = "Warning",
                Title = "Płatność do potwierdzenia",
                Message = $"{p.Client.FirstName} {p.Client.LastName}: {p.Amount} {p.Currency}",
                ClientId = p.ClientId,
                ClientName = p.Client.FirstName + " " + p.Client.LastName,
                PaymentId = p.Id,
                LocationId = p.Client.LocationId,
                LocationName = p.Client.Location.Name,
                CreatedAt = p.CreatedAt,
                ActionUrl = $"/clients/{p.ClientId}/workspace"
            })
            .ToListAsync();
    }

    private async Task<List<OperationalAlertDto>> BuildClientWithoutPackageAlertsAsync(
        OperationalAlertFilterDto filter,
        int? trainerId)
    {
        var query = _context.Clients
            .Include(c => c.Location)
            .Where(c =>
                !c.IsDeleted &&
                c.Status != "Inactive" &&
                !_context.ClientPackages.Any(cp => cp.ClientId == c.Id && cp.IsActive));

        query = ApplyClientScope(query, filter.LocationId, trainerId);

        return await query
            .OrderByDescending(c => c.CreatedAt)
            .Take(20)
            .Select(c => new OperationalAlertDto
            {
                Type = "ClientWithoutActivePackage",
                Severity = "Warning",
                Title = "Klient bez aktywnego pakietu",
                Message = c.FirstName + " " + c.LastName,
                ClientId = c.Id,
                ClientName = c.FirstName + " " + c.LastName,
                LocationId = c.LocationId,
                LocationName = c.Location.Name,
                CreatedAt = c.CreatedAt,
                ActionUrl = $"/clients/{c.Id}/workspace"
            })
            .ToListAsync();
    }

    private async Task<List<OperationalAlertDto>> BuildPackageEndingAlertsAsync(
        OperationalAlertFilterDto filter,
        int? trainerId)
    {
        var query = _context.ClientPackages
            .Include(cp => cp.Client)
                .ThenInclude(c => c.Location)
            .Where(cp =>
                cp.IsActive &&
                cp.TotalSessions - cp.UsedSessions >= 1 &&
                cp.TotalSessions - cp.UsedSessions <= 2);

        query = ApplyClientPackageScope(query, filter.LocationId, trainerId);

        return await query
            .OrderBy(cp => cp.TotalSessions - cp.UsedSessions)
            .Take(20)
            .Select(cp => new OperationalAlertDto
            {
                Type = "PackageEndingSoon",
                Severity = "Info",
                Title = "Pakiet kończy się niedługo",
                Message = $"{cp.Client.FirstName} {cp.Client.LastName}: zostało {cp.TotalSessions - cp.UsedSessions} treningów",
                ClientId = cp.ClientId,
                ClientName = cp.Client.FirstName + " " + cp.Client.LastName,
                LocationId = cp.Client.LocationId,
                LocationName = cp.Client.Location.Name,
                CreatedAt = cp.PurchaseDate,
                DueAt = cp.ValidUntil,
                ActionUrl = $"/clients/{cp.ClientId}/workspace"
            })
            .ToListAsync();
    }

    private async Task<List<OperationalAlertDto>> BuildRenewalCancellationAlertsAsync(
        OperationalAlertFilterDto filter,
        int? trainerId)
    {
        var query = _context.Clients
            .Include(c => c.Location)
            .Where(c =>
                !c.IsDeleted &&
                c.RenewalCancellationRequestedAt != null &&
                c.RenewalCancelledAt == null);

        query = ApplyClientScope(query, filter.LocationId, trainerId);

        return await query
            .OrderBy(c => c.RenewalCancellationRequestedAt)
            .Take(20)
            .Select(c => new OperationalAlertDto
            {
                Type = "RenewalCancellationRequested",
                Severity = "Warning",
                Title = "Klient zgłosił zakończenie odnowienia",
                Message = c.FirstName + " " + c.LastName,
                ClientId = c.Id,
                ClientName = c.FirstName + " " + c.LastName,
                LocationId = c.LocationId,
                LocationName = c.Location.Name,
                CreatedAt = c.RenewalCancellationRequestedAt ?? c.UpdatedAt,
                ActionUrl = $"/clients/{c.Id}/workspace"
            })
            .ToListAsync();
    }

    private async Task<List<OperationalAlertDto>> BuildOutlookSyncAlertsAsync(
        OperationalAlertFilterDto filter,
        int? trainerId)
    {
        var now = DateTime.UtcNow;
        var query = _context.Sessions
            .Include(s => s.Location)
            .Include(s => s.Trainer)
                .ThenInclude(t => t.User)
            .Where(s =>
                !s.IsDeleted &&
                s.Status != "Cancelled" &&
                s.StartAt >= now.AddDays(-1) &&
                s.StartAt <= now.AddDays(30) &&
                !_context.CalendarEventLinks.Any(l => l.SessionId == s.Id && l.Provider == "Outlook"));

        query = ApplySessionScope(query, filter.LocationId, trainerId);

        return await query
            .OrderBy(s => s.StartAt)
            .Take(20)
            .Select(s => new OperationalAlertDto
            {
                Type = "SessionNotSyncedToOutlook",
                Severity = "Warning",
                Title = "Sesja nie jest zsynchronizowana z Outlookiem",
                Message = s.Title,
                SessionId = s.Id,
                TrainerId = s.TrainerId,
                TrainerName = s.Trainer.User.FirstName + " " + s.Trainer.User.LastName,
                LocationId = s.LocationId,
                LocationName = s.Location.Name,
                CreatedAt = s.CreatedAt,
                DueAt = s.StartAt,
                ActionUrl = $"/sessions/{s.Id}/workspace"
            })
            .ToListAsync();
    }

    private async Task<List<OperationalAlertDto>> BuildLocationLimitAlertsAsync(
        OperationalAlertFilterDto filter,
        int? trainerId)
    {
        var now = DateTime.UtcNow;
        var sessions = await ApplySessionScope(
                _context.Sessions
                    .Include(s => s.Location)
                    .Include(s => s.Participants)
                    .Where(s =>
                        !s.IsDeleted &&
                        s.Status != "Cancelled" &&
                        s.StartAt >= now &&
                        s.StartAt <= now.AddDays(14)),
                filter.LocationId,
                trainerId)
            .OrderBy(s => s.StartAt)
            .Take(100)
            .ToListAsync();

        var alerts = new List<OperationalAlertDto>();

        foreach (var session in sessions)
        {
            var overlapping = sessions
                .Where(s =>
                    s.LocationId == session.LocationId &&
                    s.StartAt < session.EndAt &&
                    s.EndAt > session.StartAt)
                .ToList();

            var peopleCount =
                overlapping.Select(s => s.TrainerId).Distinct().Count() +
                overlapping.Sum(s => s.Participants.Count);

            if (peopleCount <= LocationPeopleLimit)
                continue;

            alerts.Add(new OperationalAlertDto
            {
                Type = "LocationLimitExceeded",
                Severity = "Critical",
                Title = "Przekroczony limit lokalizacji",
                Message = $"{session.Location.Name}: {peopleCount}/{LocationPeopleLimit} osób",
                SessionId = session.Id,
                LocationId = session.LocationId,
                LocationName = session.Location.Name,
                CreatedAt = session.CreatedAt,
                DueAt = session.StartAt,
                ActionUrl = $"/sessions/{session.Id}/workspace"
            });
        }

        return alerts
            .DistinctBy(a => a.SessionId)
            .Take(20)
            .ToList();
    }

    private async Task<List<OperationalAlertDto>> BuildInvitationAlertsAsync(
        OperationalAlertFilterDto filter,
        int? trainerId)
    {
        var now = DateTime.UtcNow;
        var query = _context.Invitations
            .Include(i => i.Location)
            .Where(i => !i.IsAccepted && i.CancelledAt == null);

        if (!_currentUser.IsOwner && _currentUser.UserId.HasValue)
            query = query.Where(i => i.CreatedBy == _currentUser.UserId.Value);

        if (filter.LocationId.HasValue)
            query = query.Where(i => i.LocationId == filter.LocationId.Value);

        var invitations = await query
            .OrderBy(i => i.ExpiresAt)
            .Take(30)
            .ToListAsync();

        return invitations
            .Where(i => i.ExpiresAt <= now || i.CreatedAt <= now.AddDays(-1))
            .Select(i => new OperationalAlertDto
            {
                Type = i.ExpiresAt <= now ? "InvitationExpired" : "InvitationPending",
                Severity = i.ExpiresAt <= now ? "Warning" : "Info",
                Title = i.ExpiresAt <= now ? "Zaproszenie wygasło" : "Zaproszenie oczekuje",
                Message = $"{i.Email} ({i.Role})",
                InvitationId = i.Id,
                LocationId = i.LocationId,
                LocationName = i.Location.Name,
                CreatedAt = i.CreatedAt,
                DueAt = i.ExpiresAt,
                ActionUrl = $"/invitations/{i.Id}"
            })
            .ToList();
    }

    private IQueryable<ClientPayment> ApplyClientScope(
        IQueryable<ClientPayment> query,
        int? locationId,
        int? trainerId)
    {
        if (locationId.HasValue)
            query = query.Where(p => p.Client.LocationId == locationId.Value);

        if (trainerId.HasValue)
            query = query.Where(p => p.Client.TrainerId == trainerId.Value);

        return query;
    }

    private IQueryable<Client> ApplyClientScope(
        IQueryable<Client> query,
        int? locationId,
        int? trainerId)
    {
        if (locationId.HasValue)
            query = query.Where(c => c.LocationId == locationId.Value);

        if (trainerId.HasValue)
            query = query.Where(c => c.TrainerId == trainerId.Value);

        return query;
    }

    private IQueryable<ClientPackage> ApplyClientPackageScope(
        IQueryable<ClientPackage> query,
        int? locationId,
        int? trainerId)
    {
        if (locationId.HasValue)
            query = query.Where(cp => cp.Client.LocationId == locationId.Value);

        if (trainerId.HasValue)
            query = query.Where(cp => cp.Client.TrainerId == trainerId.Value);

        return query;
    }

    private IQueryable<Session> ApplySessionScope(
        IQueryable<Session> query,
        int? locationId,
        int? trainerId)
    {
        if (locationId.HasValue)
            query = query.Where(s => s.LocationId == locationId.Value);

        if (trainerId.HasValue)
            query = query.Where(s => s.TrainerId == trainerId.Value);

        return query;
    }

    private async Task<int?> GetCurrentTrainerIdAsync()
    {
        if (!_currentUser.IsTrainer || !_currentUser.UserId.HasValue)
            return null;

        return await _context.Trainers
            .Where(t => t.UserId == _currentUser.UserId.Value)
            .Select(t => (int?)t.Id)
            .FirstOrDefaultAsync();
    }

    private static int SeverityRank(string severity)
    {
        return severity switch
        {
            "Critical" => 3,
            "Warning" => 2,
            _ => 1
        };
    }
}
