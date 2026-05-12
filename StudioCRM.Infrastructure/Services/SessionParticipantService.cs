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

        if (session.Status == "Completed")
            throw new InvalidOperationException("Cannot add participant to completed session.");

        var client = await _context.Clients.FirstOrDefaultAsync(c => c.Id == request.ClientId);

        if (client is null)
            throw new InvalidOperationException("Client does not exist.");

        var alreadyExists = await _context.SessionParticipants
            .AnyAsync(sp => sp.SessionId == sessionId && sp.ClientId == request.ClientId);

        if (alreadyExists)
            throw new InvalidOperationException("Client is already assigned to this session.");

        var activeClientPackage = await ResolveActiveClientPackageAsync(client.Id);
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

        if (participant.Session.Status == "Completed")
            throw new InvalidOperationException("Cannot remove participant from completed session.");

        _context.SessionParticipants.Remove(participant);
        await _context.SaveChangesAsync();

        await TrySyncSessionToOutlookAsync(sessionId);

        return true;
    }

    public async Task<bool> CompleteSessionAsync(int sessionId, CompleteSessionDto request)
    {
        var session = await _context.Sessions
            .Include(s => s.Participants)
            .FirstOrDefaultAsync(s => s.Id == sessionId);

        if (session is null)
            return false;

        if (session.Status == "Cancelled")
            throw new InvalidOperationException("Cancelled session cannot be completed.");

        if (session.Status == "Completed")
            throw new InvalidOperationException("Session is already completed.");

        if (!request.Participants.Any())
            throw new InvalidOperationException("At least one participant is required.");

        if (!Enum.TryParse<SessionBillingType>(request.ActualSessionType, out var actualBillingType))
            throw new InvalidOperationException("Invalid actual session type.");

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

            var previouslyCountedPackageId = participant.ClientPackageId;
            var wasPreviouslyCounted = participant.IsCountedFromPackage;
            var previouslyCharged = wasPreviouslyCounted
                ? Math.Max(0, participant.SessionsCharged)
                : 0;
            var requestedSessionsCharged = NormalizeSessionsCharged(participantRequest.SessionsCharged);

            participant.AttendanceStatus = participantRequest.AttendanceStatus;
            participant.CountsAgainstPackage = participantRequest.CountsAgainstPackage;
            participant.SessionsCharged = requestedSessionsCharged;
            participant.Note = participantRequest.Note;

            if (participantRequest.CountsAgainstPackage && participantRequest.AttendanceStatus == "Present")
            {
                var activeClientPackage = await _context.ClientPackages
                    .Where(cp => cp.ClientId == client.Id && cp.IsActive)
                    .OrderByDescending(cp => cp.PurchaseDate)
                    .FirstOrDefaultAsync();

                if (activeClientPackage is not null)
                {
                    var usedSessions = await _context.SessionParticipants
                        .Where(p =>
                            p.ClientPackageId == activeClientPackage.Id &&
                            p.IsCountedFromPackage)
                        .SumAsync(p => p.SessionsCharged);

                    var sessionsCharged = requestedSessionsCharged;
                    var isAlreadyCountedThisPackage = previouslyCountedPackageId == activeClientPackage.Id;
                    var newUsedSessions = usedSessions - (isAlreadyCountedThisPackage ? previouslyCharged : 0) + sessionsCharged;

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

        if (pricePackage is null)
        {
            throw new InvalidOperationException(
                $"No price package found for {actualBillingType}, {sessionsPerWeek} sessions per week and location {locationId}.");
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

    private async Task<ClientPackage?> ResolveActiveClientPackageAsync(int clientId)
    {
        return await _context.ClientPackages
            .Where(cp => cp.ClientId == clientId && cp.IsActive)
            .OrderByDescending(cp => cp.PurchaseDate)
            .FirstOrDefaultAsync();
    }

    private static int NormalizeSessionsCharged(int value)
    {
        return Math.Max(1, value);
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
