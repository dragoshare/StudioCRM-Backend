using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using StudioCRM.Application.DTOs.SessionParticipants;
using StudioCRM.Application.Interfaces;
using StudioCRM.Application.Interfaces.Calendar;
using StudioCRM.Domain.Entities;
using StudioCRM.Domain.Enums;
using StudioCRM.Infrastructure.Persistence;

namespace StudioCRM.Infrastructure.Services;

public class SessionParticipantService : ISessionParticipantService
{
    private readonly StudioCRMDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly ISubscriptionService _subscriptionService;
    private readonly IOutlookCalendarSyncService _outlookCalendarSyncService;
    private readonly ILogger<SessionParticipantService> _logger;

    public SessionParticipantService(
        StudioCRMDbContext context,
        ICurrentUserService currentUser,
        ISubscriptionService subscriptionService,
        IOutlookCalendarSyncService outlookCalendarSyncService,
        ILogger<SessionParticipantService> logger)
    {
        _context = context;
        _currentUser = currentUser;
        _subscriptionService = subscriptionService;
        _outlookCalendarSyncService = outlookCalendarSyncService;
        _logger = logger;
    }

    public async Task<List<SessionParticipantDto>> GetBySessionIdAsync(int sessionId)
    {
        return await _context.SessionParticipants
            .Include(sp => sp.Client)
            .Include(sp => sp.Package)
            .Where(sp => sp.SessionId == sessionId)
            .OrderBy(sp => sp.Client.LastName)
            .ThenBy(sp => sp.Client.FirstName)
            .Select(sp => new SessionParticipantDto
            {
                Id = sp.Id,
                SessionId = sp.SessionId,
                ClientId = sp.ClientId,
                ClientFullName = sp.Client.FirstName + " " + sp.Client.LastName,
                PackageId = sp.PackageId,
                PackageName = sp.Package != null ? sp.Package.Name : null,
                AttendanceStatus = sp.AttendanceStatus,
                CountsAgainstPackage = sp.CountsAgainstPackage,
                SessionsCharged = sp.SessionsCharged,
                Note = sp.Note
            })
            .ToListAsync();
    }

    public async Task<SessionParticipantDto> AddParticipantAsync(int sessionId, AddSessionParticipantDto request)
    {
        var session = await _context.Sessions.FirstOrDefaultAsync(s => s.Id == sessionId);

        if (session is null)
            throw new InvalidOperationException("Session does not exist.");

        await EnsureCurrentUserCanManageSessionAsync(session);

        if (session.Status == "Completed")
            throw new InvalidOperationException("Cannot add participant to completed session.");

        var client = await _context.Clients.FirstOrDefaultAsync(c => c.Id == request.ClientId);

        if (client is null)
            throw new InvalidOperationException("Client does not exist.");

        var alreadyExists = await _context.SessionParticipants
            .AnyAsync(sp => sp.SessionId == sessionId && sp.ClientId == request.ClientId);

        if (alreadyExists)
            throw new InvalidOperationException("Client is already assigned to this session.");

        var activeClientPackage = await ResolveActiveClientPackageAsync(
            client.Id,
            session.PlannedSessionType,
            session.LocationId);
        var sessionsCharged = NormalizeSessionsCharged(request.SessionsCharged);

        var participant = new SessionParticipant
        {
            SessionId = sessionId,
            ClientId = client.Id,
            PackageId = activeClientPackage?.PackageId,
            ClientPackageId = activeClientPackage?.Id,
            AttendanceStatus = "Planned",
            CountsAgainstPackage = request.CountsAgainstPackage,
            SessionsCharged = sessionsCharged,
            PlannedBillingType = activeClientPackage?.ExpectedBillingType,
            ExpectedUnitPrice = activeClientPackage?.ExpectedUnitPrice,
            Note = request.Note,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        await _context.SessionParticipants.AddAsync(participant);


        session.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        await TrySyncSessionToOutlookAsync(sessionId);

        return await MapParticipantAsync(participant.Id);
    }

    public async Task<bool> RemoveParticipantAsync(int sessionId, int participantId)
    {
        var participant = await _context.SessionParticipants
            .Include(sp => sp.Session)
            .FirstOrDefaultAsync(sp => sp.Id == participantId && sp.SessionId == sessionId);

        if (participant is null)
            return false;

        await EnsureCurrentUserCanManageSessionAsync(participant.Session);

        if (participant.Session.Status == "Completed")
            throw new InvalidOperationException("Cannot remove participant from completed session.");

        _context.SessionParticipants.Remove(participant);
        await _context.SaveChangesAsync();

        await TrySyncSessionToOutlookAsync(sessionId);

        return true;
    }

    public async Task<bool> CompleteSessionAsync(int sessionId, CompleteSessionDto request)
    {
        return await CompleteSessionAsync(sessionId, request, skipUserAccessCheck: false);
    }

    public async Task<bool> CompleteSessionAutomaticallyAsync(int sessionId, CompleteSessionDto request)
    {
        return await CompleteSessionAsync(sessionId, request, skipUserAccessCheck: true);
    }

    private async Task<bool> CompleteSessionAsync(
        int sessionId,
        CompleteSessionDto request,
        bool skipUserAccessCheck)
    {
        var session = await _context.Sessions
            .Include(s => s.Participants)
            .FirstOrDefaultAsync(s => s.Id == sessionId);

        if (session is null)
            return false;

        if (!skipUserAccessCheck)
        {
            await EnsureCurrentUserCanManageSessionAsync(session);
        }

        await EnsureSessionIsNotLockedByPaidSettlementAsync(session.TrainerId, session.StartAt);

        if (session.Status == "Cancelled")
            throw new InvalidOperationException("Cancelled session cannot be completed.");

        if (!request.Participants.Any())
            throw new InvalidOperationException("At least one participant is required.");

        if (!Enum.TryParse<SessionBillingType>(request.ActualSessionType, out var actualBillingType))
            throw new InvalidOperationException("Invalid actual session type.");

        var updatesCompletedSession =
            session.Status == "Completed" ||
            session.Participants.Any(p => p.IsCountedFromPackage);

        if (updatesCompletedSession)
        {
            await RevertSessionPackageAccountingAsync(session);

            var requestedClientIds = request.Participants
                .Select(p => p.ClientId)
                .ToHashSet();

            var participantsToRemove = session.Participants
                .Where(p => !requestedClientIds.Contains(p.ClientId))
                .ToList();

            _context.SessionParticipants.RemoveRange(participantsToRemove);
        }

        var completedPackageIds = new HashSet<int>();

        foreach (var participantRequest in request.Participants)
        {
            var client = await _context.Clients
                .FirstOrDefaultAsync(c => c.Id == participantRequest.ClientId);

            if (client is null)
                throw new InvalidOperationException($"Client {participantRequest.ClientId} does not exist.");

            var participant = await _context.SessionParticipants
                .FirstOrDefaultAsync(sp =>
                    sp.SessionId == sessionId &&
                    sp.ClientId == participantRequest.ClientId);

            if (participant is null)
            {
                participant = new SessionParticipant
                {
                    SessionId = sessionId,
                    ClientId = client.Id,
                    CreatedAt = DateTime.UtcNow
                };

                await _context.SessionParticipants.AddAsync(participant);
            }

            var requestedSessionsCharged = NormalizeSessionsCharged(participantRequest.SessionsCharged);

            participant.AttendanceStatus = participantRequest.AttendanceStatus;
            participant.CountsAgainstPackage = participantRequest.CountsAgainstPackage;
            participant.SessionsCharged = requestedSessionsCharged;
            participant.Note = participantRequest.Note;

            if (participantRequest.CountsAgainstPackage && participantRequest.AttendanceStatus == "Present")
            {
                var activeClientPackage = await _context.ClientPackages
                    .Where(cp =>
                        cp.ClientId == client.Id &&
                        cp.IsActive &&
                        (actualBillingType == SessionBillingType.Group
                            ? cp.ExpectedBillingType == SessionBillingType.Group
                            : cp.ExpectedBillingType != SessionBillingType.Group) &&
                        (cp.LocationId == null || cp.LocationId == session.LocationId))
                    .OrderByDescending(cp => cp.PurchaseDate)
                    .FirstOrDefaultAsync();

                if (activeClientPackage is not null)
                {
                    var usedSessions = await _context.SessionParticipants
                        .Where(p =>
                            p.ClientPackageId == activeClientPackage.Id &&
                            p.IsCountedFromPackage &&
                            p.SessionId != session.Id)
                        .SumAsync(p => p.SessionsCharged);

                    var sessionsCharged = requestedSessionsCharged;
                    var newUsedSessions = usedSessions + sessionsCharged;

                    if (newUsedSessions > activeClientPackage.TotalSessions)
                        throw new InvalidOperationException("Client package has no remaining sessions.");

                    var expectedUnitPrice = activeClientPackage.ExpectedUnitPrice;
                    var actualUnitPrice = await ResolveActualUnitPriceAsync(
                        activeClientPackage,
                        session.LocationId,
                        actualBillingType);
                    var balanceDifference = (expectedUnitPrice - actualUnitPrice) * sessionsCharged;

                    participant.PackageId = activeClientPackage.PackageId;
                    participant.ClientPackageId = activeClientPackage.Id;
                    participant.SessionsCharged = sessionsCharged;
                    participant.PlannedBillingType = activeClientPackage.ExpectedBillingType;
                    participant.ActualBillingType = actualBillingType;
                    participant.ExpectedUnitPrice = expectedUnitPrice;
                    participant.ActualUnitPrice = actualUnitPrice;
                    participant.BalanceDifference = balanceDifference;
                    participant.IsCountedFromPackage = true;

                    var existingTransaction = await _context.ClientBalanceTransactions
                        .FirstOrDefaultAsync(t =>
                            t.ClientPackageId == activeClientPackage.Id &&
                            t.SessionId == session.Id &&
                            t.ClientId == activeClientPackage.ClientId &&
                            t.Type == BalanceTransactionType.PackageAdjustment);

                    if (existingTransaction is not null)
                    {
                        _context.ClientBalanceTransactions.Remove(existingTransaction);
                    }

                    if (balanceDifference != 0)
                    {
                        await _context.ClientBalanceTransactions.AddAsync(new ClientBalanceTransaction
                        {
                            ClientId = activeClientPackage.ClientId,
                            ClientPackageId = activeClientPackage.Id,
                            SessionId = session.Id,
                            Amount = balanceDifference,
                            Type = BalanceTransactionType.PackageAdjustment,
                            Description = balanceDifference > 0
                                ? $"Nadpłata: sesja rozliczona taniej niż pakiet ({actualBillingType} zamiast {activeClientPackage.ExpectedBillingType})."
                                : $"Dopłata: sesja rozliczona drożej niż pakiet ({actualBillingType} zamiast {activeClientPackage.ExpectedBillingType}).",
                            CreatedAt = DateTime.UtcNow
                        });
                    }

                    activeClientPackage.UsedSessions = newUsedSessions;

                    if (newUsedSessions >= activeClientPackage.TotalSessions)
                        completedPackageIds.Add(activeClientPackage.Id);
                }
                else
                {
                    throw new InvalidOperationException(
                        $"Client {client.Id} does not have an active subscription package.");
                }
            }
            else
            {
                participant.CountsAgainstPackage = false;
                participant.IsCountedFromPackage = false;
                participant.ClientPackageId = null;
                participant.PackageId = null;
                participant.PlannedBillingType = null;
                participant.ActualBillingType = null;
                participant.ExpectedUnitPrice = null;
                participant.ActualUnitPrice = null;
                participant.BalanceDifference = null;
            }

            participant.UpdatedAt = DateTime.UtcNow;
        }

        var presentCount = request.Participants
            .Count(p => p.AttendanceStatus == "Present");

        session.Status = "Completed";
        session.ActualSessionType = request.ActualSessionType;
        session.ActualParticipantsCount = presentCount;
        session.CompletedAt = DateTime.UtcNow;
        session.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        foreach (var clientPackageId in completedPackageIds)
        {
            await _subscriptionService.RenewAfterCompletedCycleAsync(clientPackageId);
        }

        await TrySyncSessionToOutlookAsync(sessionId);

        return true;
    }

    private async Task TrySyncSessionToOutlookAsync(int sessionId)
    {
        try
        {
            await _outlookCalendarSyncService.SyncSessionAsync(sessionId);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not sync session {SessionId} participants to Outlook.", sessionId);
        }
    }

    private async Task<decimal> ResolveActualUnitPriceAsync(
        ClientPackage clientPackage,
        int sessionLocationId,
        SessionBillingType actualBillingType)
    {
        var sessionsPerWeek = clientPackage.SessionsPerWeek > 0
            ? clientPackage.SessionsPerWeek
            : Math.Max(1, (int)Math.Ceiling(clientPackage.TotalSessions / 4m));
        var locationId = clientPackage.LocationId ?? sessionLocationId;

        var pricePackage = await _context.Packages
            .Where(p =>
                p.IsActive &&
                p.BillingType == actualBillingType &&
                p.SessionsPerWeek == sessionsPerWeek &&
                p.SessionsLimit == clientPackage.TotalSessions &&
                (p.LocationId == locationId || p.LocationId == null))
            .OrderByDescending(p => p.LocationId == locationId)
            .FirstOrDefaultAsync();

        pricePackage ??= await _context.Packages
            .Where(p =>
                p.IsActive &&
                p.BillingType == actualBillingType &&
                p.SessionsLimit == clientPackage.TotalSessions &&
                (p.LocationId == locationId || p.LocationId == null))
            .OrderByDescending(p => p.LocationId == locationId)
            .ThenBy(p => Math.Abs(p.SessionsPerWeek - sessionsPerWeek))
            .FirstOrDefaultAsync();

        if (pricePackage is null)
        {
            throw new InvalidOperationException(
                $"No price package found for {actualBillingType}, {clientPackage.TotalSessions} sessions and location {locationId}.");
        }

        if (pricePackage.SessionsLimit <= 0)
            throw new InvalidOperationException("Matched price package sessions limit must be greater than zero.");

        return NormalizeUnitPrice(pricePackage.Price / pricePackage.SessionsLimit);
    }

    private static decimal NormalizeUnitPrice(decimal amount)
    {
        if (amount <= 0)
            throw new InvalidOperationException("Actual unit price must be greater than zero.");

        return decimal.Round(amount, 2);
    }

    private async Task EnsureSessionIsNotLockedByPaidSettlementAsync(int trainerId, DateTime startAt)
    {
        var (year, month) = GetStudioYearMonth(startAt);

        var isPaid = await _context.TrainerMonthlySettlements.AnyAsync(s =>
            s.TrainerId == trainerId &&
            s.Year == year &&
            s.Month == month &&
            s.IsPaid);

        if (isPaid)
        {
            throw new InvalidOperationException(
                "Session cannot be changed because the trainer settlement for this month has already been paid.");
        }
    }

    private async Task EnsureCurrentUserCanManageSessionAsync(Session session)
    {
        if (_currentUser.IsOwner)
            return;

        if (!_currentUser.IsTrainer || !_currentUser.UserId.HasValue)
            throw new InvalidOperationException("Current user cannot manage this session.");

        var ownsSession = await _context.Trainers
            .AnyAsync(t => t.Id == session.TrainerId && t.UserId == _currentUser.UserId.Value);

        if (!ownsSession)
            throw new InvalidOperationException("Trainer can manage only their own sessions.");
    }

    private async Task RevertSessionPackageAccountingAsync(Session session)
    {
        var countedPackageGroups = session.Participants
            .Where(p => p.IsCountedFromPackage && p.ClientPackageId.HasValue)
            .GroupBy(p => p.ClientPackageId!.Value)
            .Select(g => new
            {
                ClientPackageId = g.Key,
                SessionsCharged = g.Sum(p => Math.Max(0, p.SessionsCharged))
            })
            .ToList();

        foreach (var countedPackage in countedPackageGroups)
        {
            var clientPackage = await _context.ClientPackages
                .FirstOrDefaultAsync(cp => cp.Id == countedPackage.ClientPackageId);

            if (clientPackage is not null)
            {
                clientPackage.UsedSessions = Math.Max(
                    0,
                    clientPackage.UsedSessions - countedPackage.SessionsCharged);
            }
        }

        var adjustments = await _context.ClientBalanceTransactions
            .Where(t =>
                t.SessionId == session.Id &&
                t.Type == BalanceTransactionType.PackageAdjustment)
            .ToListAsync();

        _context.ClientBalanceTransactions.RemoveRange(adjustments);

        foreach (var participant in session.Participants)
        {
            participant.CountsAgainstPackage = false;
            participant.IsCountedFromPackage = false;
            participant.ClientPackageId = null;
            participant.PackageId = null;
            participant.PlannedBillingType = null;
            participant.ActualBillingType = null;
            participant.ExpectedUnitPrice = null;
            participant.ActualUnitPrice = null;
            participant.BalanceDifference = null;
            participant.UpdatedAt = DateTime.UtcNow;
        }
    }

    private async Task<ClientPackage?> ResolveActiveClientPackageAsync(
        int clientId,
        string? plannedSessionType,
        int sessionLocationId)
    {
        var isGroupSession = string.Equals(
            plannedSessionType,
            SessionBillingType.Group.ToString(),
            StringComparison.OrdinalIgnoreCase);

        return await _context.ClientPackages
            .Where(cp =>
                cp.ClientId == clientId &&
                cp.IsActive &&
                (isGroupSession
                    ? cp.ExpectedBillingType == SessionBillingType.Group
                    : cp.ExpectedBillingType != SessionBillingType.Group) &&
                (cp.LocationId == null || cp.LocationId == sessionLocationId))
            .OrderByDescending(cp => cp.PurchaseDate)
            .FirstOrDefaultAsync();
    }

    private static int NormalizeSessionsCharged(int value)
    {
        return Math.Max(1, value);
    }

    private static (int Year, int Month) GetStudioYearMonth(DateTime value)
    {
        var utc = value.Kind == DateTimeKind.Utc
            ? value
            : DateTime.SpecifyKind(value, DateTimeKind.Utc);

        var local = TimeZoneInfo.ConvertTimeFromUtc(utc, GetStudioTimeZone());
        return (local.Year, local.Month);
    }

    private static TimeZoneInfo GetStudioTimeZone()
    {
        foreach (var id in new[] { "Europe/Warsaw", "Central European Standard Time" })
        {
            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById(id);
            }
            catch (TimeZoneNotFoundException)
            {
            }
            catch (InvalidTimeZoneException)
            {
            }
        }

        return TimeZoneInfo.Utc;
    }

    private async Task<SessionParticipantDto> MapParticipantAsync(int participantId)
    {
        return await _context.SessionParticipants
            .Include(sp => sp.Client)
            .Include(sp => sp.Package)
            .Where(sp => sp.Id == participantId)
            .Select(sp => new SessionParticipantDto
            {
                Id = sp.Id,
                SessionId = sp.SessionId,
                ClientId = sp.ClientId,
                ClientFullName = sp.Client.FirstName + " " + sp.Client.LastName,
                PackageId = sp.PackageId,
                PackageName = sp.Package != null ? sp.Package.Name : null,
                AttendanceStatus = sp.AttendanceStatus,
                CountsAgainstPackage = sp.CountsAgainstPackage,
                SessionsCharged = sp.SessionsCharged,
                Note = sp.Note
            })
            .FirstAsync();
    }
}
