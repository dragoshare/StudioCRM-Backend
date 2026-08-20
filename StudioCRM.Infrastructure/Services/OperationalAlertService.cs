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
    private const int ContractReminderDays = 30;
    private const int ContractRenewalWarningDays = 14;
    private const int AcceptedClientAlertDays = 7;
    private const int SettlementReminderStartDay = 25;
    private const int SettlementReminderCarryOverDays = 5;
    private const string WindowsStudioTimeZone = "Central European Standard Time";
    private const string IanaStudioTimeZone = "Europe/Warsaw";

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
        alerts.AddRange(await BuildTrainerContractAlertsAsync(filter));
        alerts.AddRange(await BuildAcceptedClientAlertsAsync(filter, trainerId));
        alerts.AddRange(await BuildTrainerSettlementReminderAlertsAsync(filter));
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

    private async Task<List<OperationalAlertDto>> BuildTrainerContractAlertsAsync(
        OperationalAlertFilterDto filter)
    {
        if (!_currentUser.IsOwner)
            return new List<OperationalAlertDto>();

        var now = DateTime.UtcNow;
        var from = now.AddDays(-ContractRenewalWarningDays);
        var to = now.AddDays(ContractReminderDays);

        var query = _context.TrainerContracts
            .Include(c => c.Trainer)
                .ThenInclude(t => t.User)
            .Include(c => c.ContractLocations)
                .ThenInclude(cl => cl.Location)
            .Where(c =>
                c.IsActive &&
                c.ValidTo != null &&
                c.ValidTo >= from &&
                c.ValidTo <= to);

        if (filter.LocationId.HasValue)
            query = query.Where(c => c.ContractLocations.Any(cl => cl.LocationId == filter.LocationId.Value));

        var contracts = await query
            .OrderBy(c => c.ValidTo)
            .Take(50)
            .ToListAsync();

        if (contracts.Count == 0)
            return new List<OperationalAlertDto>();

        var trainerIds = contracts
            .Select(c => c.TrainerId)
            .Distinct()
            .ToList();

        var replacements = await _context.TrainerContracts
            .Include(c => c.ContractLocations)
            .Where(c =>
                c.IsActive &&
                trainerIds.Contains(c.TrainerId) &&
                c.ValidFrom <= to.AddDays(1) &&
                (c.ValidTo == null || c.ValidTo >= now))
            .ToListAsync();

        var alerts = new List<OperationalAlertDto>();

        foreach (var contract in contracts)
        {
            var validTo = contract.ValidTo!.Value;
            var locationIds = contract.ContractLocations
                .Select(cl => cl.LocationId)
                .Distinct()
                .ToList();
            var hasReplacement = replacements.Any(r =>
                r.Id != contract.Id &&
                r.TrainerId == contract.TrainerId &&
                r.ValidFrom <= validTo.AddDays(1) &&
                (r.ValidTo == null || r.ValidTo >= validTo) &&
                CoversAllLocations(r, locationIds));

            if (validTo < now && hasReplacement)
                continue;

            var needsRenewal = !hasReplacement && validTo <= now.AddDays(ContractRenewalWarningDays);
            var locations = string.Join(", ",
                contract.ContractLocations
                    .Select(cl => cl.Location.Name)
                    .OrderBy(name => name));
            var trainerName = $"{contract.Trainer.User.FirstName} {contract.Trainer.User.LastName}";

            alerts.Add(new OperationalAlertDto
            {
                Type = needsRenewal ? "TrainerContractNeedsRenewal" : "TrainerContractEndingSoon",
                Severity = needsRenewal ? "Warning" : "Info",
                Title = needsRenewal
                    ? "Umowa trenera wymaga odnowienia"
                    : "Umowa trenera kończy się niedługo",
                Message = BuildContractAlertMessage(trainerName, contract.ContractNumber, validTo, locations, needsRenewal),
                TrainerId = contract.TrainerId,
                TrainerName = trainerName,
                TrainerContractId = contract.Id,
                LocationId = filter.LocationId ?? (locationIds.Count == 1 ? locationIds[0] : null),
                LocationName = filter.LocationId.HasValue
                    ? contract.ContractLocations.FirstOrDefault(cl => cl.LocationId == filter.LocationId.Value)?.Location.Name
                    : (contract.ContractLocations.Count == 1 ? contract.ContractLocations.First().Location.Name : locations),
                CreatedAt = contract.CreatedAt,
                DueAt = validTo,
                ActionUrl = $"/trainers/{contract.TrainerId}/contracts"
            });
        }

        return alerts
            .Take(20)
            .ToList();
    }

    private async Task<List<OperationalAlertDto>> BuildAcceptedClientAlertsAsync(
        OperationalAlertFilterDto filter,
        int? trainerId)
    {
        var since = DateTime.UtcNow.AddDays(-AcceptedClientAlertDays);
        var query = _context.Invitations
            .Include(i => i.Location)
            .Where(i =>
                i.Role == "Client" &&
                i.IsAccepted &&
                i.AcceptedAt != null &&
                i.AcceptedAt >= since);

        if (!_currentUser.IsOwner && _currentUser.UserId.HasValue)
            query = query.Where(i => i.CreatedBy == _currentUser.UserId.Value);

        if (filter.LocationId.HasValue)
            query = query.Where(i => i.LocationId == filter.LocationId.Value);

        var invitations = await query
            .OrderByDescending(i => i.AcceptedAt)
            .Take(20)
            .ToListAsync();

        if (invitations.Count == 0)
            return new List<OperationalAlertDto>();

        var emails = invitations
            .Select(i => i.Email)
            .Distinct()
            .ToList();

        var clients = await _context.Clients
            .Include(c => c.Location)
            .Where(c =>
                !c.IsDeleted &&
                emails.Contains(c.Email))
            .ToListAsync();

        if (trainerId.HasValue)
            clients = clients
                .Where(c => c.TrainerId == trainerId.Value || c.CreatedBy == _currentUser.UserId)
                .ToList();

        return invitations
            .Select(i =>
            {
                var client = clients.FirstOrDefault(c =>
                    c.Email == i.Email &&
                    c.LocationId == i.LocationId);
                var clientName = client is null
                    ? i.Email
                    : $"{client.FirstName} {client.LastName}";

                return new OperationalAlertDto
                {
                    Type = "ClientInvitationAccepted",
                    Severity = "Info",
                    Title = "Nowy klient zaakceptował zaproszenie",
                    Message = clientName,
                    ClientId = client?.Id,
                    ClientName = clientName,
                    InvitationId = i.Id,
                    LocationId = i.LocationId,
                    LocationName = i.Location.Name,
                    CreatedAt = i.AcceptedAt ?? i.CreatedAt,
                    ActionUrl = client is null
                        ? $"/invitations/{i.Id}"
                        : $"/clients/{client.Id}/workspace"
                };
            })
            .ToList();
    }

    private async Task<List<OperationalAlertDto>> BuildTrainerSettlementReminderAlertsAsync(
        OperationalAlertFilterDto filter)
    {
        if (!_currentUser.IsOwner)
            return new List<OperationalAlertDto>();

        var targetMonth = ResolveSettlementReminderMonth(GetStudioNow());
        if (targetMonth is null)
            return new List<OperationalAlertDto>();

        var (year, month) = targetMonth.Value;
        var from = GetStudioMonthStartUtc(year, month);
        var nextYear = month == 12 ? year + 1 : year;
        var nextMonth = month == 12 ? 1 : month + 1;
        var to = GetStudioMonthStartUtc(nextYear, nextMonth);

        var sessionsQuery = _context.Sessions
            .Include(s => s.Trainer)
                .ThenInclude(t => t.User)
            .Include(s => s.Location)
            .Where(s =>
                !s.IsDeleted &&
                s.Status == "Completed" &&
                s.StartAt >= from &&
                s.StartAt < to);

        if (filter.LocationId.HasValue)
            sessionsQuery = sessionsQuery.Where(s => s.LocationId == filter.LocationId.Value);

        var sessions = await sessionsQuery
            .OrderBy(s => s.Trainer.User.LastName)
            .ThenBy(s => s.Trainer.User.FirstName)
            .ThenBy(s => s.StartAt)
            .ToListAsync();

        if (sessions.Count == 0)
            return new List<OperationalAlertDto>();

        var trainerIds = sessions
            .Select(s => s.TrainerId)
            .Distinct()
            .ToList();

        var contracts = await _context.TrainerContracts
            .Include(c => c.ContractLocations)
            .Where(c =>
                c.IsActive &&
                trainerIds.Contains(c.TrainerId) &&
                c.ValidFrom < to &&
                (c.ValidTo == null || c.ValidTo >= from))
            .ToListAsync();

        var settlements = await _context.TrainerMonthlySettlements
            .Where(s =>
                trainerIds.Contains(s.TrainerId) &&
                s.Year == year &&
                s.Month == month)
            .ToListAsync();

        var contractedSessions = sessions
            .Where(s => HasContractCoverage(contracts, s))
            .GroupBy(s => s.TrainerId)
            .ToList();

        var dueAt = to.AddTicks(-1);
        var now = DateTime.UtcNow;
        var alerts = new List<OperationalAlertDto>();

        foreach (var group in contractedSessions)
        {
            var settlement = settlements.FirstOrDefault(s => s.TrainerId == group.Key);
            if (settlement?.IsPaid == true)
                continue;

            var firstSession = group.First();
            var trainerName = $"{firstSession.Trainer.User.FirstName} {firstSession.Trainer.User.LastName}";
            var totalHours = group.Sum(s => (decimal)(s.EndAt - s.StartAt).TotalHours);

            alerts.Add(new OperationalAlertDto
            {
                Type = "TrainerSettlementReminder",
                Severity = settlement is null ? "Warning" : "Info",
                Title = "Przypomnienie o rozliczeniu trenera",
                Message = $"{trainerName}: {group.Count()} sesji / {totalHours:0.##} h za {year}-{month:00}",
                TrainerId = group.Key,
                TrainerName = trainerName,
                SettlementId = settlement?.Id,
                Year = year,
                Month = month,
                CreatedAt = now,
                DueAt = dueAt,
                ActionUrl = $"/trainers/{group.Key}/settlement?year={year}&month={month}"
            });
        }

        return alerts
            .OrderBy(a => a.TrainerName)
            .Take(20)
            .ToList();
    }

    private static bool CoversAllLocations(TrainerContract contract, List<int> locationIds)
    {
        if (locationIds.Count == 0)
            return false;

        var contractLocationIds = contract.ContractLocations
            .Select(cl => cl.LocationId)
            .ToHashSet();

        return locationIds.All(contractLocationIds.Contains);
    }

    private static string BuildContractAlertMessage(
        string trainerName,
        string contractNumber,
        DateTime validTo,
        string locations,
        bool needsRenewal)
    {
        var baseMessage = $"{trainerName}: umowa {contractNumber} do {validTo:yyyy-MM-dd}";

        if (!string.IsNullOrWhiteSpace(locations))
            baseMessage += $" ({locations})";

        return needsRenewal
            ? $"{baseMessage}. Brak kolejnej umowy dla tego zakresu."
            : baseMessage;
    }

    private static bool HasContractCoverage(List<TrainerContract> contracts, Session session)
    {
        return contracts.Any(c =>
            c.TrainerId == session.TrainerId &&
            c.ContractLocations.Any(cl => cl.LocationId == session.LocationId));
    }

    private static (int Year, int Month)? ResolveSettlementReminderMonth(DateTime studioNow)
    {
        var daysInMonth = DateTime.DaysInMonth(studioNow.Year, studioNow.Month);

        if (studioNow.Day >= Math.Min(SettlementReminderStartDay, daysInMonth))
            return (studioNow.Year, studioNow.Month);

        if (studioNow.Day <= SettlementReminderCarryOverDays)
        {
            var previousMonth = studioNow.AddMonths(-1);
            return (previousMonth.Year, previousMonth.Month);
        }

        return null;
    }

    private static DateTime GetStudioNow()
    {
        return TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, GetStudioTimeZone());
    }

    private static DateTime GetStudioMonthStartUtc(int year, int month)
    {
        var localStart = new DateTime(year, month, 1, 0, 0, 0, DateTimeKind.Unspecified);
        return TimeZoneInfo.ConvertTimeToUtc(localStart, GetStudioTimeZone());
    }

    private static TimeZoneInfo GetStudioTimeZone()
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(WindowsStudioTimeZone);
        }
        catch (Exception ex) when (ex is TimeZoneNotFoundException or InvalidTimeZoneException)
        {
            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById(IanaStudioTimeZone);
            }
            catch (Exception fallbackEx) when (fallbackEx is TimeZoneNotFoundException or InvalidTimeZoneException)
            {
                return TimeZoneInfo.Utc;
            }
        }
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
