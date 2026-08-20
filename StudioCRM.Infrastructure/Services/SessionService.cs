using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using StudioCRM.Application.DTOs.Calendar;
using StudioCRM.Application.DTOs.SessionParticipants;
using StudioCRM.Application.DTOs.Sessions;
using StudioCRM.Application.Interfaces;
using StudioCRM.Application.Interfaces.Calendar;
using StudioCRM.Domain.Entities;
using StudioCRM.Domain.Enums;
using StudioCRM.Infrastructure.Persistence;
using StudioCRM.Application.Common;
namespace StudioCRM.Infrastructure.Services;

public class SessionService : ISessionService
{
    private const int LocationPeopleLimit = 8;
    private const string OutlookStudioTimeZone = "Central European Standard Time";

    private readonly StudioCRMDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly ISubscriptionService _subscriptionService;
    private readonly ISessionParticipantService _sessionParticipantService;
    private readonly IOutlookCalendarSyncService _outlookCalendarSyncService;
    private readonly IStudioSettingsService _settingsService;
    private readonly ILogger<SessionService> _logger;

    public SessionService(
        StudioCRMDbContext context,
        ICurrentUserService currentUser,
        ISubscriptionService subscriptionService,
        ISessionParticipantService sessionParticipantService,
        IOutlookCalendarSyncService outlookCalendarSyncService,
        IStudioSettingsService settingsService,
        ILogger<SessionService> logger)
    {
        _context = context;
        _currentUser = currentUser;
        _subscriptionService = subscriptionService;
        _sessionParticipantService = sessionParticipantService;
        _outlookCalendarSyncService = outlookCalendarSyncService;
        _settingsService = settingsService;
        _logger = logger;
    }

    public async Task<List<SessionDto>> GetAllAsync()
    {
        var query = await ApplySessionVisibilityAsync(BaseQuery());

        var sessions = await query
            .OrderBy(s => s.StartAt)
            .ToListAsync();

        return await MapSessionDtosAsync(sessions);
    }

    public async Task<SessionDto?> GetByIdAsync(int id)
    {
        var query = await ApplySessionVisibilityAsync(BaseQuery());

        var session = await query
            .FirstOrDefaultAsync(s => s.Id == id);

        if (session is null)
            return null;

        return await MapSessionDtoAsync(session);
    }

    public async Task<SessionWorkspaceDto?> GetWorkspaceAsync(int id)
    {
        var session = await GetByIdAsync(id);

        if (session is null)
            return null;

        var link = await _context.CalendarEventLinks
            .FirstOrDefaultAsync(x =>
                x.SessionId == id &&
                x.Provider == "Outlook");

        var externalEvent = link is null
            ? null
            : await _context.ExternalCalendarEvents
                .FirstOrDefaultAsync(x =>
                    x.Provider == link.Provider &&
                    x.ExternalEventId == link.ExternalEventId);

        var isLockedByPaidSettlement = await IsSessionLockedByPaidSettlementAsync(
            session.TrainerId,
            session.StartAt);

        return new SessionWorkspaceDto
        {
            Session = session,
            OutlookSync = new SessionOutlookSyncDto
            {
                IsSynced = link is not null,
                Provider = link?.Provider,
                ExternalEventId = link?.ExternalEventId,
                LastSyncedAt = link?.SyncedAt,
                Warnings = ReadStringList(externalEvent?.MappingWarningsJson)
            },
            QuickActions = new SessionWorkspaceQuickActionsDto
            {
                CanEditParticipants = !isLockedByPaidSettlement,
                CanComplete = !isLockedByPaidSettlement && session.Status != "Cancelled"
            }
        };
    }

    public async Task<SessionDto> CreateAsync(CreateSessionDto request)
    {
        var normalizedStartAt = NormalizeStudioDateTime(request.StartAt);
        var normalizedEndAt = await ResolveCreateSessionEndAtAsync(request.EndAt, normalizedStartAt);
        var requestedStatus = string.IsNullOrWhiteSpace(request.Status)
            ? "Planned"
            : request.Status;

        if (requestedStatus == "Completed")
        {
            await EnsureSessionIsNotLockedByPaidSettlementAsync(request.TrainerId, normalizedStartAt);
        }

        await ValidateSessionRequestAsync(
            request.TrainerId,
            request.LocationId,
            normalizedStartAt,
            normalizedEndAt,
            request.Participants,
            excludedSessionId: null);

        var clients = await GetClientsForParticipantsAsync(request.Participants);
        var outlookCategories = await ResolveOutlookCategoriesForTrainerAsync(
            request.TrainerId,
            request.OutlookCategories);
        var outlookCategoryColors = await ResolveOutlookCategoryColorsForTrainerAsync(
            request.TrainerId,
            outlookCategories);

        var title = string.IsNullOrWhiteSpace(request.Title)
            ? SessionTitleBuilder.Build(clients)
            : request.Title;

        var transaction = requestedStatus == "Completed"
            ? await _context.Database.BeginTransactionAsync()
            : null;
        var transactionCommitted = false;

        try
        {
            var session = new Session
            {
                Title = title,
                Note = request.Note,
                StartAt = normalizedStartAt,
                EndAt = normalizedEndAt,
                TrainerId = request.TrainerId,
                LocationId = request.LocationId,
                StudioRoom = null,
                Status = requestedStatus,
                PlannedSessionType = request.PlannedSessionType ?? ResolveSessionType(request.Participants.Count),
                OutlookCategoriesJson = SerializeOutlookCategories(outlookCategories),
                OutlookCategoryColorsJson = SerializeOutlookCategoryColors(outlookCategoryColors),
                PrimaryOutlookCategory = GetPrimaryOutlookCategory(outlookCategories),
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                CreatedBy = _currentUser.UserId
            };

            await _context.Sessions.AddAsync(session);
            await _context.SaveChangesAsync();

            await AddParticipantsToSessionAsync(session.Id, request.Participants, clients);

            if (requestedStatus == "Completed")
            {
                var completionRequest = BuildCompletionRequest(request);
                var completed = await _sessionParticipantService.CompleteSessionAsync(session.Id, completionRequest);

                if (!completed)
                    throw new InvalidOperationException("Created session could not be completed.");

                if (transaction is not null)
                {
                    await transaction.CommitAsync();
                    transactionCommitted = true;
                }
            }
            else if (requestedStatus != "Cancelled")
            {
                await TrySyncSessionToOutlookAsync(session.Id);
            }

            return await GetByIdAsync(session.Id)
                ?? throw new InvalidOperationException("Created session could not be loaded.");
        }
        catch
        {
            if (transaction is not null && !transactionCommitted)
                await transaction.RollbackAsync();

            throw;
        }
        finally
        {
            if (transaction is not null)
                await transaction.DisposeAsync();
        }
    }

    public async Task<SessionDto?> UpdateAsync(int id, UpdateSessionDto request)
    {
        var normalizedStartAt = NormalizeStudioDateTime(request.StartAt);
        var normalizedEndAt = NormalizeStudioDateTime(request.EndAt);

        var session = await _context.Sessions
            .Include(s => s.Participants)
            .FirstOrDefaultAsync(s => s.Id == id);

        if (session is null)
            return null;

        var requestedStatus = string.IsNullOrWhiteSpace(request.Status)
            ? "Planned"
            : request.Status;

        var currentHasPackageAccounting = session.Participants.Any(p => p.IsCountedFromPackage);

        var locksCurrentSettlement =
            session.Status == "Completed" ||
            currentHasPackageAccounting;

        var locksTargetSettlement = requestedStatus == "Completed";

        if (locksCurrentSettlement)
        {
            await EnsureSessionIsNotLockedByPaidSettlementAsync(session.TrainerId, session.StartAt);
        }

        if (locksTargetSettlement)
        {
            await EnsureSessionIsNotLockedByPaidSettlementAsync(request.TrainerId, normalizedStartAt);
        }

        await ValidateSessionRequestAsync(
            request.TrainerId,
            request.LocationId,
            normalizedStartAt,
            normalizedEndAt,
            request.Participants,
            excludedSessionId: id);

        var clients = await GetClientsForParticipantsAsync(request.Participants);
        var outlookCategories = await ResolveOutlookCategoriesForTrainerAsync(
            request.TrainerId,
            request.OutlookCategories);
        var outlookCategoryColors = await ResolveOutlookCategoryColorsForTrainerAsync(
            request.TrainerId,
            outlookCategories);

        var transaction = requestedStatus == "Completed"
            ? await _context.Database.BeginTransactionAsync()
            : null;
        var transactionCommitted = false;

        try
        {
            session.Title = string.IsNullOrWhiteSpace(request.Title)
                ? SessionTitleBuilder.Build(clients)
                : request.Title;

            session.Note = request.Note;
            session.StartAt = normalizedStartAt;
            session.EndAt = normalizedEndAt;
            session.TrainerId = request.TrainerId;
            session.LocationId = request.LocationId;
            session.StudioRoom = null;
            session.Status = requestedStatus;
            session.PlannedSessionType = request.PlannedSessionType ?? ResolveSessionType(request.Participants.Count);
            session.OutlookCategoriesJson = SerializeOutlookCategories(outlookCategories);
            session.OutlookCategoryColorsJson = SerializeOutlookCategoryColors(outlookCategoryColors);
            session.PrimaryOutlookCategory = GetPrimaryOutlookCategory(outlookCategories);
            session.UpdatedAt = DateTime.UtcNow;

            if (currentHasPackageAccounting && requestedStatus == "Completed")
            {
                EnsureCompletedSessionParticipantsWereNotChanged(session, request.Participants);

                await _context.SaveChangesAsync();
                await TrySyncSessionToOutlookAsync(session.Id);

                if (transaction is not null)
                {
                    await transaction.CommitAsync();
                    transactionCommitted = true;
                }

                return await GetByIdAsync(session.Id);
            }

            if (locksCurrentSettlement)
            {
                await RevertSessionPackageAccountingAsync(session);
                session.ActualSessionType = null;
                session.ActualParticipantsCount = null;
                session.CompletedAt = null;
            }

            _context.SessionParticipants.RemoveRange(session.Participants);

            await _context.SaveChangesAsync();

            await AddParticipantsToSessionAsync(session.Id, request.Participants, clients);

            if (requestedStatus == "Completed")
            {
                var completionRequest = BuildCompletionRequest(request);
                var completed = await _sessionParticipantService.CompleteSessionAsync(session.Id, completionRequest);

                if (!completed)
                    throw new InvalidOperationException("Updated session could not be completed.");

                if (transaction is not null)
                {
                    await transaction.CommitAsync();
                    transactionCommitted = true;
                }
            }
            else if (requestedStatus == "Cancelled")
            {
                await TryDeleteSessionFromOutlookAsync(session.Id);
            }
            else
            {
                await TrySyncSessionToOutlookAsync(session.Id);
            }

            return await GetByIdAsync(session.Id);
        }
        catch
        {
            if (transaction is not null && !transactionCommitted)
                await transaction.RollbackAsync();

            throw;
        }
        finally
        {
            if (transaction is not null)
                await transaction.DisposeAsync();
        }
    }

    public async Task<bool> DeleteAsync(int id)
    {
        var session = await _context.Sessions.FirstOrDefaultAsync(s => s.Id == id);

        if (session is null)
            return false;

        var touchesCompletedSession =
            session.Status == "Completed" ||
            await _context.SessionParticipants.AnyAsync(p =>
                p.SessionId == session.Id &&
                p.IsCountedFromPackage);

        if (touchesCompletedSession)
        {
            await EnsureSessionIsNotLockedByPaidSettlementAsync(session.TrainerId, session.StartAt);
            await RevertSessionPackageAccountingAsync(session);
        }

        await TryDeleteSessionFromOutlookAsync(session.Id);

        _context.Sessions.Remove(session);
        await _context.SaveChangesAsync();

        return true;
    }

    public async Task<bool> RestoreAsync(int id)
    {
        var session = await _context.Sessions
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(s => s.Id == id);

        if (session is null)
            return false;

        session.Status = "Planned";
        session.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        await TrySyncSessionToOutlookAsync(session.Id);

        return true;
    }

    public async Task<List<SessionDto>> GetDeletedAsync()
    {
        var sessions = await BaseQuery()
            .IgnoreQueryFilters()
            .Where(s => s.Status == "Deleted")
            .OrderByDescending(s => s.UpdatedAt)
            .ToListAsync();

        return await MapSessionDtosAsync(sessions);
    }

    public async Task<List<SessionDto>> GetFilteredAsync(SessionFilterDto filter)
    {
        var query = await ApplySessionVisibilityAsync(BaseQuery());

        if (filter.TrainerId.HasValue)
            query = query.Where(s => s.TrainerId == filter.TrainerId.Value);

        if (filter.ClientId.HasValue)
            query = query.Where(s => s.Participants.Any(p => p.ClientId == filter.ClientId.Value));

        if (filter.LocationId.HasValue)
            query = query.Where(s => s.LocationId == filter.LocationId.Value);

        if (!string.IsNullOrWhiteSpace(filter.Status))
            query = query.Where(s => s.Status == filter.Status);

        if (filter.From.HasValue)
        {
            var from = NormalizeStudioDateTime(filter.From.Value);
            query = query.Where(s => s.StartAt >= from);
        }

        if (filter.To.HasValue)
        {
            var to = NormalizeStudioDateTime(filter.To.Value);
            query = query.Where(s => s.StartAt <= to);
        }

        var sessions = await query
            .OrderBy(s => s.StartAt)
            .ToListAsync();

        return await MapSessionDtosAsync(sessions);
    }

    public async Task CountSessionFromPackageAsync(CountSessionFromPackageRequest request)
    {
        var participant = await _context.SessionParticipants
            .Include(p => p.Session)
            .FirstOrDefaultAsync(p => p.Id == request.SessionParticipantId);

        if (participant is null)
            throw new InvalidOperationException("Session participant not found.");

        await EnsureCurrentUserCanManageSessionAsync(participant.Session);

        await EnsureSessionIsNotLockedByPaidSettlementAsync(
            participant.Session.TrainerId,
            participant.Session.StartAt);

        var clientPackage = await _context.ClientPackages
            .FirstOrDefaultAsync(p => p.Id == request.ClientPackageId);

        if (clientPackage is null)
            throw new InvalidOperationException("Client package not found.");

        if (participant.ClientId != clientPackage.ClientId)
            throw new InvalidOperationException("This package does not belong to this client.");

        if (!clientPackage.IsActive)
            throw new InvalidOperationException("Client package is not active.");

        var usedSessions = await _context.SessionParticipants
            .Where(p =>
                p.ClientPackageId == clientPackage.Id &&
                p.IsCountedFromPackage)
            .SumAsync(p => p.SessionsCharged);

        var isAlreadyCountedThisParticipant =
            participant.ClientPackageId == clientPackage.Id &&
            participant.IsCountedFromPackage;

        var previousCharged = isAlreadyCountedThisParticipant ? Math.Max(0, participant.SessionsCharged) : 0;
        var sessionsCharged = 1;
        var newUsedSessions = usedSessions - previousCharged + sessionsCharged;

        if (newUsedSessions > clientPackage.TotalSessions)
            throw new InvalidOperationException("Client package has no remaining sessions.");

        var expectedUnitPrice = clientPackage.ExpectedUnitPrice;
        var actualUnitPrice = request.ActualUnitPrice.HasValue
            ? NormalizeUnitPrice(request.ActualUnitPrice.Value)
            : await ResolveActualUnitPriceAsync(clientPackage, participant.Session.LocationId, request.ActualBillingType);
        var balanceDifference = expectedUnitPrice - actualUnitPrice;

        participant.ClientPackageId = clientPackage.Id;
        participant.CountsAgainstPackage = true;
        participant.SessionsCharged = sessionsCharged;
        participant.PackageId = clientPackage.PackageId;

        participant.PlannedBillingType = clientPackage.ExpectedBillingType;
        participant.ActualBillingType = request.ActualBillingType;
        participant.ExpectedUnitPrice = expectedUnitPrice;
        participant.ActualUnitPrice = actualUnitPrice;
        participant.BalanceDifference = balanceDifference;
        participant.IsCountedFromPackage = true;

        participant.UpdatedAt = DateTime.UtcNow;

        participant.Session.ActualSessionType = request.ActualBillingType.ToString();
        participant.Session.ActualParticipantsCount = await _context.SessionParticipants
            .CountAsync(p => p.SessionId == participant.SessionId);
        participant.Session.Status = "Completed";
        participant.Session.CompletedAt ??= DateTime.UtcNow;
        participant.Session.UpdatedAt = DateTime.UtcNow;

        var existingTransaction = await _context.ClientBalanceTransactions
            .FirstOrDefaultAsync(t =>
                t.ClientPackageId == clientPackage.Id &&
                t.SessionId == participant.SessionId &&
                t.ClientId == clientPackage.ClientId &&
                t.Type == BalanceTransactionType.PackageAdjustment);

        if (existingTransaction is not null)
        {
            _context.ClientBalanceTransactions.Remove(existingTransaction);
        }

        if (balanceDifference != 0)
        {
            var transaction = new ClientBalanceTransaction
            {
                ClientId = clientPackage.ClientId,
                ClientPackageId = clientPackage.Id,
                SessionId = participant.SessionId,
                Amount = balanceDifference,
                Type = BalanceTransactionType.PackageAdjustment,
                Description = balanceDifference > 0
                    ? $"Nadpłata: sesja rozliczona taniej niż pakiet ({request.ActualBillingType} zamiast {clientPackage.ExpectedBillingType})."
                    : $"Dopłata: sesja rozliczona drożej niż pakiet ({request.ActualBillingType} zamiast {clientPackage.ExpectedBillingType}).",
                CreatedAt = DateTime.UtcNow
            };

            await _context.ClientBalanceTransactions.AddAsync(transaction);
        }

        clientPackage.UsedSessions = newUsedSessions;

        if (clientPackage.UsedSessions >= clientPackage.TotalSessions)
            await _subscriptionService.RenewAfterCompletedCycleAsync(clientPackage.Id);

        await _context.SaveChangesAsync();
    }

    private async Task<decimal> ResolveActualUnitPriceAsync(
        ClientPackage clientPackage,
        int sessionLocationId,
        SessionBillingType actualBillingType)
    {
        var sessionsPerWeek = ResolveSessionsPerWeek(clientPackage);
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

    private async Task EnsureSessionIsNotLockedByPaidSettlementAsync(int trainerId, DateTime startAt)
    {
        if (await IsSessionLockedByPaidSettlementAsync(trainerId, startAt))
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

    private async Task<bool> IsSessionLockedByPaidSettlementAsync(int trainerId, DateTime startAt)
    {
        var (year, month) = GetStudioYearMonth(startAt);

        return await _context.TrainerMonthlySettlements.AnyAsync(s =>
            s.TrainerId == trainerId &&
            s.Year == year &&
            s.Month == month &&
            s.IsPaid);
    }

    private async Task RevertSessionPackageAccountingAsync(Session session)
    {
        var participants = await _context.SessionParticipants
            .Where(p => p.SessionId == session.Id)
            .ToListAsync();

        var countedPackageGroups = participants
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

        foreach (var participant in participants)
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

    private static void EnsureCompletedSessionParticipantsWereNotChanged(
        Session session,
        List<CreateSessionParticipantDto> requestedParticipants)
    {
        var existingClientIds = session.Participants
            .Select(p => p.ClientId)
            .OrderBy(id => id)
            .ToList();

        var requestedClientIds = requestedParticipants
            .Select(p => p.ClientId)
            .OrderBy(id => id)
            .ToList();

        if (!existingClientIds.SequenceEqual(requestedClientIds))
        {
            throw new InvalidOperationException(
                "Use session completion to change participants or package accounting for a completed session.");
        }
    }

    private static int ResolveSessionsPerWeek(ClientPackage clientPackage)
    {
        return clientPackage.SessionsPerWeek > 0
            ? clientPackage.SessionsPerWeek
            : Math.Max(1, (int)Math.Ceiling(clientPackage.TotalSessions / 4m));
    }

    private static decimal NormalizeUnitPrice(decimal amount)
    {
        if (amount <= 0)
            throw new InvalidOperationException("Actual unit price must be greater than zero.");

        return decimal.Round(amount, 2);
    }

    private IQueryable<Session> BaseQuery()
    {
        return _context.Sessions
            .Include(s => s.Trainer)
                .ThenInclude(t => t.User)
            .Include(s => s.Location)
            .Include(s => s.Participants)
                .ThenInclude(p => p.Client)
            .Include(s => s.Participants)
                .ThenInclude(p => p.Package)
            .Include(s => s.Participants)
                .ThenInclude(p => p.ClientPackage);
    }

    private async Task<IQueryable<Session>> ApplySessionVisibilityAsync(IQueryable<Session> query)
    {
        if (_currentUser.IsOwner)
            return query;

        if (!_currentUser.IsTrainer || !_currentUser.UserId.HasValue)
            return query.Where(s => false);

        var locationIds = await _context.TrainerLocations
            .Where(tl => tl.Trainer.UserId == _currentUser.UserId.Value)
            .Select(tl => tl.LocationId)
            .ToListAsync();

        return locationIds.Count == 0
            ? query.Where(s => false)
            : query.Where(s => locationIds.Contains(s.LocationId));
    }

    private async Task ValidateSessionRequestAsync(
        int trainerId,
        int locationId,
        DateTime startAt,
        DateTime endAt,
        List<CreateSessionParticipantDto> participants,
        int? excludedSessionId)
    {
        if (endAt <= startAt)
            throw new InvalidOperationException("End date must be later than start date.");

        var trainerExists = await _context.Trainers.AnyAsync(t => t.Id == trainerId);
        if (!trainerExists)
            throw new InvalidOperationException("Trainer does not exist.");

        var locationExists = await _context.Locations.AnyAsync(l => l.Id == locationId);
        if (!locationExists)
            throw new InvalidOperationException("Location does not exist.");

        if (participants is null || !participants.Any())
            throw new InvalidOperationException("At least one participant is required.");

        var clientIds = participants.Select(p => p.ClientId).ToList();
        var distinctClientIds = clientIds.Distinct().ToList();

        if (clientIds.Count != distinctClientIds.Count)
            throw new InvalidOperationException("Duplicated clients in session participants.");

        if (distinctClientIds.Count > 4)
            throw new InvalidOperationException("A trainer can run a session for at most four participants.");

        var duplicateSlotSession = await _context.Sessions
            .Where(s =>
                s.TrainerId == trainerId &&
                s.Status != "Cancelled" &&
                s.StartAt == startAt &&
                s.EndAt == endAt &&
                (!excludedSessionId.HasValue || s.Id != excludedSessionId.Value))
            .OrderBy(s => s.StartAt)
            .Select(s => new
            {
                s.Id,
                s.StartAt,
                s.EndAt
            })
            .FirstOrDefaultAsync();

        if (duplicateSlotSession is not null)
        {
            var start = ToStudioDisplayDateTime(duplicateSlotSession.StartAt);
            var end = ToStudioDisplayDateTime(duplicateSlotSession.EndAt);

            throw new InvalidOperationException(
                $"Trainer already has a session in this exact time slot ({start:yyyy-MM-dd HH:mm}-{end:HH:mm}, session #{duplicateSlotSession.Id}). Add participants to the existing session instead of creating another one.");
        }
    }

    private async Task<List<Client>> GetClientsForParticipantsAsync(List<CreateSessionParticipantDto> participants)
    {
        var clientIds = participants.Select(p => p.ClientId).Distinct().ToList();

        var clients = await _context.Clients
            .Where(c => clientIds.Contains(c.Id))
            .ToListAsync();

        if (clients.Count != clientIds.Count)
            throw new InvalidOperationException("One or more clients do not exist.");

        return participants
            .Select(p => clients.First(c => c.Id == p.ClientId))
            .ToList();
    }

    private async Task AddParticipantsToSessionAsync(
        int sessionId,
        List<CreateSessionParticipantDto> participants,
        List<Client> clients)
    {
        foreach (var participantRequest in participants)
        {
            var client = clients.First(c => c.Id == participantRequest.ClientId);
            var activeClientPackage = await ResolveActiveClientPackageAsync(client.Id);
            var sessionsCharged = NormalizeSessionsCharged(participantRequest.SessionsCharged);

            var participant = new SessionParticipant
            {
                SessionId = sessionId,
                ClientId = client.Id,
                PackageId = activeClientPackage?.PackageId,
                ClientPackageId = activeClientPackage?.Id,
                AttendanceStatus = "Planned",
                CountsAgainstPackage = participantRequest.CountsAgainstPackage,
                SessionsCharged = sessionsCharged,
                PlannedBillingType = activeClientPackage?.ExpectedBillingType,
                ExpectedUnitPrice = activeClientPackage?.ExpectedUnitPrice,
                Note = participantRequest.Note,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            await _context.SessionParticipants.AddAsync(participant);
        }

        await _context.SaveChangesAsync();
    }

    private static CompleteSessionDto BuildCompletionRequest(CreateSessionDto request)
    {
        var actualSessionType = ResolveActualSessionTypeForCompletion(request);

        return new CompleteSessionDto
        {
            ActualSessionType = actualSessionType.ToString(),
            Participants = request.Participants
                .Select(p => new CompleteSessionParticipantDto
                {
                    ClientId = p.ClientId,
                    AttendanceStatus = "Present",
                    CountsAgainstPackage = p.CountsAgainstPackage,
                    SessionsCharged = NormalizeSessionsCharged(p.SessionsCharged),
                    Note = p.Note
                })
                .ToList()
        };
    }

    private async Task<DateTime> ResolveCreateSessionEndAtAsync(DateTime? endAt, DateTime normalizedStartAt)
    {
        if (endAt.HasValue)
            return NormalizeStudioDateTime(endAt.Value);

        var settings = await _settingsService.GetOwnerSettingsAsync();
        return normalizedStartAt.AddMinutes(settings.DefaultSessionDurationMinutes);
    }

    private static CompleteSessionDto BuildCompletionRequest(UpdateSessionDto request)
    {
        var actualSessionType = ResolveActualSessionTypeForCompletion(request);

        return new CompleteSessionDto
        {
            ActualSessionType = actualSessionType.ToString(),
            Participants = request.Participants
                .Select(p => new CompleteSessionParticipantDto
                {
                    ClientId = p.ClientId,
                    AttendanceStatus = "Present",
                    CountsAgainstPackage = p.CountsAgainstPackage,
                    SessionsCharged = NormalizeSessionsCharged(p.SessionsCharged),
                    Note = p.Note
                })
                .ToList()
        };
    }

    private static SessionBillingType ResolveActualSessionTypeForCompletion(CreateSessionDto request)
    {
        if (!string.IsNullOrWhiteSpace(request.PlannedSessionType) &&
            Enum.TryParse<SessionBillingType>(request.PlannedSessionType, out var plannedSessionType))
        {
            return plannedSessionType;
        }

        return request.Participants.Count switch
        {
            1 => SessionBillingType.OneToOne,
            2 => SessionBillingType.TwoToOne,
            3 => SessionBillingType.ThreeToOne,
            4 => SessionBillingType.FourToOne,
            _ => throw new InvalidOperationException("Completed session can have at most four billable participants.")
        };
    }

    private static SessionBillingType ResolveActualSessionTypeForCompletion(UpdateSessionDto request)
    {
        if (!string.IsNullOrWhiteSpace(request.PlannedSessionType) &&
            Enum.TryParse<SessionBillingType>(request.PlannedSessionType, out var plannedSessionType))
        {
            return plannedSessionType;
        }

        return request.Participants.Count switch
        {
            1 => SessionBillingType.OneToOne,
            2 => SessionBillingType.TwoToOne,
            3 => SessionBillingType.ThreeToOne,
            4 => SessionBillingType.FourToOne,
            _ => throw new InvalidOperationException("Completed session can have at most four billable participants.")
        };
    }

    private async Task<ClientPackage?> ResolveActiveClientPackageAsync(int clientId)
    {
        return await _context.ClientPackages
            .Where(cp => cp.ClientId == clientId && cp.IsActive)
            .OrderByDescending(cp => cp.PurchaseDate)
            .FirstOrDefaultAsync();
    }

    private async Task TrySyncSessionToOutlookAsync(int sessionId)
    {
        try
        {
            await _outlookCalendarSyncService.SyncSessionAsync(sessionId);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not sync session {SessionId} to Outlook.", sessionId);
        }
    }

    private async Task TryDeleteSessionFromOutlookAsync(int sessionId)
    {
        try
        {
            await _outlookCalendarSyncService.DeleteSessionEventAsync(sessionId);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not delete Outlook event for session {SessionId}.", sessionId);
        }
    }

    private static DateTime NormalizeStudioDateTime(DateTime value)
    {
        return value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.ToUniversalTime(),
            _ => TimeZoneInfo.ConvertTimeToUtc(
                DateTime.SpecifyKind(value, DateTimeKind.Unspecified),
                GetStudioTimeZone())
        };
    }

    private static TimeZoneInfo GetStudioTimeZone()
    {
        foreach (var id in new[] { "Europe/Warsaw", OutlookStudioTimeZone })
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

    private static (int Year, int Month) GetStudioYearMonth(DateTime value)
    {
        var utc = value.Kind == DateTimeKind.Utc
            ? value
            : DateTime.SpecifyKind(value, DateTimeKind.Utc);

        var local = TimeZoneInfo.ConvertTimeFromUtc(utc, GetStudioTimeZone());
        return (local.Year, local.Month);
    }

    private async Task<List<SessionDto>> MapSessionDtosAsync(List<Session> sessions)
    {
        var result = new List<SessionDto>();

        foreach (var session in sessions)
        {
            result.Add(await MapSessionDtoAsync(session));
        }

        return result;
    }

    private async Task<SessionDto> MapSessionDtoAsync(Session s)
    {
        var participants = s.Participants
            .OrderBy(p => p.Client.FirstName)
            .ThenBy(p => p.Client.LastName)
            .ToList();
        var outlookCategories = ReadStringList(s.OutlookCategoriesJson);
        var outlookCategoryColors = ReadOutlookCategoryColors(
            s.OutlookCategoryColorsJson,
            outlookCategories,
            s.Trainer.OutlookCategoryName,
            s.Trainer.OutlookCategoryColor);
        var primaryOutlookCategory = s.PrimaryOutlookCategory ?? outlookCategories.FirstOrDefault();

        var locationParticipantsCount = await CountPeopleInLocationForTimeRangeAsync(
            s.LocationId,
            s.StartAt,
            s.EndAt);

        var canEdit = _currentUser.IsOwner ||
            (_currentUser.IsTrainer && s.Trainer.UserId == _currentUser.UserId);

        return new SessionDto
        {
            Id = s.Id,
            Title = s.Title,
            Note = s.Note,
            StartAt = ToStudioDisplayDateTime(s.StartAt),
            EndAt = ToStudioDisplayDateTime(s.EndAt),
            TrainerId = s.TrainerId,
            TrainerFullName = s.Trainer.User.FirstName + " " + s.Trainer.User.LastName,
            CanEdit = canEdit,
            LocationId = s.LocationId,
            LocationName = s.Location.Name,
            Status = s.Status,
            PlannedSessionType = s.PlannedSessionType,
            ActualSessionType = s.ActualSessionType,
            ActualParticipantsCount = s.ActualParticipantsCount,
            CompletedAt = s.CompletedAt.HasValue ? ToStudioDisplayDateTime(s.CompletedAt.Value) : null,
            ParticipantsCount = participants.Count,
            ClientsDisplayName = SessionTitleBuilder.Build(participants.Select(p => p.Client).ToList()),
            LocationParticipantsCount = locationParticipantsCount,
            LocationLimit = LocationPeopleLimit,
            IsLocationLimitExceeded = locationParticipantsCount > LocationPeopleLimit,
            Participants = participants.Select(p => new SessionParticipantListDto
            {
                Id = p.Id,
                ClientId = p.ClientId,
                ClientFullName = p.Client.FirstName + " " + p.Client.LastName,
                PackageId = p.PackageId,
                PackageName = p.Package != null ? p.Package.Name : null,
                ClientPackageId = p.ClientPackageId,
                AttendanceStatus = p.AttendanceStatus,
                CountsAgainstPackage = p.CountsAgainstPackage,
                IsCountedFromPackage = p.IsCountedFromPackage,
                SessionsCharged = p.SessionsCharged,
                PlannedBillingType = p.PlannedBillingType != null ? p.PlannedBillingType.ToString()! : string.Empty,
                ActualBillingType = p.ActualBillingType != null ? p.ActualBillingType.ToString()! : string.Empty,
                ExpectedUnitPrice = p.ExpectedUnitPrice ?? 0,
                ActualUnitPrice = p.ActualUnitPrice ?? 0,
                BalanceDifference = p.BalanceDifference ?? 0,
                Note = p.Note
            }).ToList(),
            CreatedAt = s.CreatedAt,
            UpdatedAt = s.UpdatedAt,
            CreatedBy = s.CreatedBy,
            OutlookCategories = outlookCategories,
            OutlookCategoryColors = outlookCategoryColors,
            PrimaryOutlookCategory = primaryOutlookCategory,
            PrimaryOutlookCategoryColor = outlookCategoryColors
                .FirstOrDefault(c => string.Equals(c.Name, primaryOutlookCategory, StringComparison.OrdinalIgnoreCase))
                ?.Color
        };
    }

    private static List<string> ReadStringList(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return new List<string>();

        try
        {
            return JsonSerializer.Deserialize<List<string>>(json) ?? new List<string>();
        }
        catch
        {
            return new List<string>();
        }
    }

    private static string? SerializeOutlookCategories(List<string>? categories)
    {
        var normalized = NormalizeOutlookCategories(categories);

        return normalized.Count == 0
            ? null
            : JsonSerializer.Serialize(normalized);
    }

    private static string? SerializeOutlookCategoryColors(List<OutlookCategoryDto>? categoryColors)
    {
        var normalized = NormalizeOutlookCategoryColors(categoryColors);

        return normalized.Count == 0
            ? null
            : JsonSerializer.Serialize(normalized);
    }

    private static string? GetPrimaryOutlookCategory(List<string>? categories)
    {
        return NormalizeOutlookCategories(categories).FirstOrDefault();
    }

    private static List<string> NormalizeOutlookCategories(List<string>? categories)
    {
        return categories?
            .Select(c => c.Trim())
            .Where(c => !string.IsNullOrWhiteSpace(c))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList() ?? new List<string>();
    }

    private static List<OutlookCategoryDto> NormalizeOutlookCategoryColors(
        List<OutlookCategoryDto>? categoryColors)
    {
        return categoryColors?
            .Select(c => new OutlookCategoryDto
            {
                Name = c.Name.Trim(),
                Color = string.IsNullOrWhiteSpace(c.Color) ? null : c.Color.Trim()
            })
            .Where(c => !string.IsNullOrWhiteSpace(c.Name))
            .GroupBy(c => c.Name, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First())
            .ToList() ?? new List<OutlookCategoryDto>();
    }

    private static List<OutlookCategoryDto> ReadOutlookCategoryColors(
        string? json,
        List<string> categories,
        string? trainerCategoryName,
        string? trainerCategoryColor)
    {
        var result = new List<OutlookCategoryDto>();

        if (!string.IsNullOrWhiteSpace(json))
        {
            try
            {
                result = JsonSerializer.Deserialize<List<OutlookCategoryDto>>(json)
                    ?? new List<OutlookCategoryDto>();
            }
            catch
            {
                result = new List<OutlookCategoryDto>();
            }
        }

        result = NormalizeOutlookCategoryColors(result);

        foreach (var category in categories)
        {
            if (result.Any(c => string.Equals(c.Name, category, StringComparison.OrdinalIgnoreCase)))
                continue;

            result.Add(new OutlookCategoryDto
            {
                Name = category,
                Color = string.Equals(category, trainerCategoryName, StringComparison.OrdinalIgnoreCase)
                    ? NormalizeOutlookCategoryColor(trainerCategoryColor)
                    : null
            });
        }

        return NormalizeOutlookCategoryColors(result);
    }

    private async Task<List<string>> ResolveOutlookCategoriesForTrainerAsync(
        int trainerId,
        List<string>? requestedCategories)
    {
        var trainerCategory = await _context.Trainers
            .Where(t => t.Id == trainerId)
            .Select(t => t.OutlookCategoryName)
            .FirstOrDefaultAsync();

        var categories = new List<string>();

        if (!string.IsNullOrWhiteSpace(trainerCategory))
            categories.Add(trainerCategory);

        categories.AddRange(requestedCategories ?? new List<string>());

        return NormalizeOutlookCategories(categories);
    }

    private async Task<List<OutlookCategoryDto>> ResolveOutlookCategoryColorsForTrainerAsync(
        int trainerId,
        List<string> categories)
    {
        var trainerCategory = await _context.Trainers
            .Where(t => t.Id == trainerId)
            .Select(t => new
            {
                t.OutlookCategoryName,
                t.OutlookCategoryColor
            })
            .FirstOrDefaultAsync();

        return categories
            .Select(category => new OutlookCategoryDto
            {
                Name = category,
                Color = trainerCategory is not null &&
                    string.Equals(category, trainerCategory.OutlookCategoryName, StringComparison.OrdinalIgnoreCase)
                    ? NormalizeOutlookCategoryColor(trainerCategory.OutlookCategoryColor)
                    : null
            })
            .ToList();
    }

    private static string? NormalizeOutlookCategoryColor(string? value)
    {
        var normalized = value?.Trim();

        return string.IsNullOrWhiteSpace(normalized)
            ? null
            : normalized;
    }

    private static DateTime ToStudioDisplayDateTime(DateTime value)
    {
        if (value.Kind == DateTimeKind.Unspecified)
            return value;

        var utc = value.Kind == DateTimeKind.Utc
            ? value
            : value.ToUniversalTime();

        return DateTime.SpecifyKind(
            TimeZoneInfo.ConvertTimeFromUtc(utc, GetStudioTimeZone()),
            DateTimeKind.Unspecified);
    }

    private async Task<int> CountPeopleInLocationForTimeRangeAsync(
        int locationId,
        DateTime startAt,
        DateTime endAt)
    {
        var overlappingSessions = await _context.Sessions
            .Where(s =>
                !s.IsDeleted &&
                s.Status != "Cancelled" &&
                s.LocationId == locationId &&
                s.StartAt < endAt &&
                s.EndAt > startAt)
            .Select(s => new
            {
                s.Id,
                s.TrainerId
            })
            .ToListAsync();

        var overlappingSessionIds = overlappingSessions
            .Select(s => s.Id)
            .ToList();

        var clientsCount = await _context.SessionParticipants
            .Where(p => overlappingSessionIds.Contains(p.SessionId))
            .CountAsync();

        var trainersCount = overlappingSessions
            .Select(s => s.TrainerId)
            .Distinct()
            .Count();

        return clientsCount + trainersCount;
    }

    

    private static string ResolveSessionType(int count)
    {
        return count switch
        {
            1 => "OneToOne",
            2 => "TwoToOne",
            3 => "ThreeToOne",
            _ => "Group"
        };
    }

    private static int NormalizeSessionsCharged(int value)
    {
        return Math.Max(1, value);
    }
}
