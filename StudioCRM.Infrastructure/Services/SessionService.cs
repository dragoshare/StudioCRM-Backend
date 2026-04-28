using Microsoft.EntityFrameworkCore;
using StudioCRM.Application.DTOs.Sessions;
using StudioCRM.Application.Interfaces;
using StudioCRM.Domain.Entities;
using StudioCRM.Domain.Enums;
using StudioCRM.Infrastructure.Persistence;

namespace StudioCRM.Infrastructure.Services;

public class SessionService : ISessionService
{
    private readonly StudioCRMDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public SessionService(
        StudioCRMDbContext context,
        ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<List<SessionDto>> GetAllAsync()
    {
        return await BaseQuery()
            .OrderBy(s => s.StartAt)
            .Select(s => MapSessionDto(s))
            .ToListAsync();
    }

    public async Task<SessionDto?> GetByIdAsync(int id)
    {
        return await BaseQuery()
            .Where(s => s.Id == id)
            .Select(s => MapSessionDto(s))
            .FirstOrDefaultAsync();
    }

    public async Task<SessionDto> CreateAsync(CreateSessionDto request)
    {
        await ValidateSessionRequestAsync(
            request.TrainerId,
            request.LocationId,
            request.StartAt,
            request.EndAt,
            request.Participants);

        var clients = await GetClientsForParticipantsAsync(request.Participants);

        var title = string.IsNullOrWhiteSpace(request.Title)
            ? BuildSessionTitle(clients)
            : request.Title;

        var session = new Session
        {
            Title = title,
            Note = request.Note,
            StartAt = request.StartAt,
            EndAt = request.EndAt,
            TrainerId = request.TrainerId,
            LocationId = request.LocationId,
            StudioRoom = request.StudioRoom,
            Status = string.IsNullOrWhiteSpace(request.Status) ? "Planned" : request.Status,
            PlannedSessionType = request.PlannedSessionType ?? ResolveSessionType(request.Participants.Count),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            CreatedBy = _currentUser.UserId
        };

        await _context.Sessions.AddAsync(session);
        await _context.SaveChangesAsync();

        await AddParticipantsToSessionAsync(session.Id, request.Participants, clients);

        return await GetByIdAsync(session.Id)
            ?? throw new InvalidOperationException("Created session could not be loaded.");
    }

    public async Task<SessionDto?> UpdateAsync(int id, UpdateSessionDto request)
    {
        var session = await _context.Sessions
            .Include(s => s.Participants)
            .FirstOrDefaultAsync(s => s.Id == id);

        if (session is null)
            return null;

        if (session.Status == "Completed")
            throw new InvalidOperationException("Completed session cannot be updated.");

        await ValidateSessionRequestAsync(
            request.TrainerId,
            request.LocationId,
            request.StartAt,
            request.EndAt,
            request.Participants);

        var clients = await GetClientsForParticipantsAsync(request.Participants);

        session.Title = string.IsNullOrWhiteSpace(request.Title)
            ? BuildSessionTitle(clients)
            : request.Title;

        session.Note = request.Note;
        session.StartAt = request.StartAt;
        session.EndAt = request.EndAt;
        session.TrainerId = request.TrainerId;
        session.LocationId = request.LocationId;
        session.StudioRoom = request.StudioRoom;
        session.Status = string.IsNullOrWhiteSpace(request.Status) ? "Planned" : request.Status;
        session.PlannedSessionType = request.PlannedSessionType ?? ResolveSessionType(request.Participants.Count);
        session.UpdatedAt = DateTime.UtcNow;

        _context.SessionParticipants.RemoveRange(session.Participants);

        await _context.SaveChangesAsync();

        await AddParticipantsToSessionAsync(session.Id, request.Participants, clients);

        return await GetByIdAsync(session.Id);
    }

    public async Task<bool> DeleteAsync(int id)
    {
        var session = await _context.Sessions.FirstOrDefaultAsync(s => s.Id == id);

        if (session is null)
            return false;

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

        return true;
    }

    public async Task<List<SessionDto>> GetDeletedAsync()
    {
        return await BaseQuery()
            .IgnoreQueryFilters()
            .Where(s => s.Status == "Deleted")
            .OrderByDescending(s => s.UpdatedAt)
            .Select(s => MapSessionDto(s))
            .ToListAsync();
    }

    public async Task<List<SessionDto>> GetFilteredAsync(SessionFilterDto filter)
    {
        var query = BaseQuery();

        if (filter.TrainerId.HasValue)
            query = query.Where(s => s.TrainerId == filter.TrainerId.Value);

        if (filter.ClientId.HasValue)
            query = query.Where(s => s.Participants.Any(p => p.ClientId == filter.ClientId.Value));

        if (filter.LocationId.HasValue)
            query = query.Where(s => s.LocationId == filter.LocationId.Value);

        if (!string.IsNullOrWhiteSpace(filter.Status))
            query = query.Where(s => s.Status == filter.Status);

        if (filter.From.HasValue)
            query = query.Where(s => s.StartAt >= filter.From.Value);

        if (filter.To.HasValue)
            query = query.Where(s => s.StartAt <= filter.To.Value);

        return await query
            .OrderBy(s => s.StartAt)
            .Select(s => MapSessionDto(s))
            .ToListAsync();
    }

    public async Task CountSessionFromPackageAsync(CountSessionFromPackageRequest request)
    {
        var participant = await _context.SessionParticipants
            .Include(p => p.Session)
            .FirstOrDefaultAsync(p => p.Id == request.SessionParticipantId);

        if (participant is null)
            throw new InvalidOperationException("Session participant not found.");

        var clientPackage = await _context.ClientPackages
            .FirstOrDefaultAsync(p => p.Id == request.ClientPackageId);

        if (clientPackage is null)
            throw new InvalidOperationException("Client package not found.");

        if (participant.ClientId != clientPackage.ClientId)
            throw new InvalidOperationException("This package does not belong to this client.");

        if (!clientPackage.IsActive)
            throw new InvalidOperationException("Client package is not active.");

        var usedSessions = await _context.SessionParticipants
            .CountAsync(p =>
                p.ClientPackageId == clientPackage.Id &&
                p.CountsAgainstPackage);

        var isAlreadyCountedThisParticipant =
            participant.ClientPackageId == clientPackage.Id &&
            participant.CountsAgainstPackage;

        if (!isAlreadyCountedThisParticipant && usedSessions >= clientPackage.TotalSessions)
            throw new InvalidOperationException("Client package has no remaining sessions.");

        var expectedUnitPrice = clientPackage.ExpectedUnitPrice;
        var actualUnitPrice = request.ActualUnitPrice;
        var balanceDifference = expectedUnitPrice - actualUnitPrice;

        participant.ClientPackageId = clientPackage.Id;
        participant.CountsAgainstPackage = true;
        participant.SessionsCharged = 1;
        participant.PackageId = clientPackage.PackageId;

        participant.PlannedBillingType = clientPackage.ExpectedBillingType;
        participant.ActualBillingType = request.ActualBillingType;
        participant.ExpectedUnitPrice = expectedUnitPrice;
        participant.ActualUnitPrice = actualUnitPrice;
        participant.BalanceDifference = balanceDifference;

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

        await _context.SaveChangesAsync();
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

    private async Task ValidateSessionRequestAsync(
        int trainerId,
        int locationId,
        DateTime startAt,
        DateTime endAt,
        List<CreateSessionParticipantDto> participants)
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

        foreach (var participant in participants)
        {
            if (participant.PackageId.HasValue)
            {
                var packageExists = await _context.Packages
                    .AnyAsync(p => p.Id == participant.PackageId.Value);

                if (!packageExists)
                    throw new InvalidOperationException($"Package {participant.PackageId.Value} does not exist.");
            }
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

            var participant = new SessionParticipant
            {
                SessionId = sessionId,
                ClientId = client.Id,
                PackageId = participantRequest.PackageId ?? client.ActivePackageId,
                AttendanceStatus = "Planned",
                CountsAgainstPackage = participantRequest.CountsAgainstPackage,
                SessionsCharged = participantRequest.SessionsCharged,
                Note = participantRequest.Note,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            await _context.SessionParticipants.AddAsync(participant);
        }

        await _context.SaveChangesAsync();
    }

    private static SessionDto MapSessionDto(Session s)
    {
        var participants = s.Participants
            .OrderBy(p => p.Client.FirstName)
            .ThenBy(p => p.Client.LastName)
            .ToList();

        return new SessionDto
        {
            Id = s.Id,
            Title = s.Title,
            Note = s.Note,
            StartAt = s.StartAt,
            EndAt = s.EndAt,
            TrainerId = s.TrainerId,
            TrainerFullName = s.Trainer.User.FirstName + " " + s.Trainer.User.LastName,
            LocationId = s.LocationId,
            LocationName = s.Location.Name,
            StudioRoom = s.StudioRoom,
            Status = s.Status,
            PlannedSessionType = s.PlannedSessionType,
            ActualSessionType = s.ActualSessionType,
            ActualParticipantsCount = s.ActualParticipantsCount,
            CompletedAt = s.CompletedAt,
            ParticipantsCount = participants.Count,
            ClientsDisplayName = BuildSessionTitle(participants.Select(p => p.Client).ToList()),
            Participants = participants.Select(p => new SessionParticipantListDto
            {
                Id = p.Id,
                ClientId = p.ClientId,
                ClientFullName = p.Client.FirstName + " " + p.Client.LastName,
                PackageId = p.PackageId,
                PackageName = p.Package != null ? p.Package.Name : null,
                AttendanceStatus = p.AttendanceStatus,
                CountsAgainstPackage = p.CountsAgainstPackage,
                SessionsCharged = p.SessionsCharged,
                Note = p.Note
            }).ToList(),
            CreatedAt = s.CreatedAt,
            UpdatedAt = s.UpdatedAt,
            CreatedBy = s.CreatedBy
        };
    }

    private static string BuildSessionTitle(List<Client> clients)
    {
        var ordered = clients
            .OrderBy(c => c.FirstName)
            .ThenBy(c => c.LastName)
            .ToList();

        if (ordered.Count == 0)
            return "Sesja";

        if (ordered.Count == 1)
            return ShortClientName(ordered[0]);

        if (ordered.Count == 2)
            return $"{ShortClientName(ordered[0])} + {ShortClientName(ordered[1])}";

        return $"{ShortClientName(ordered[0])} + {ordered.Count - 1} os.";
    }

    private static string ShortClientName(Client client)
    {
        var initial = string.IsNullOrWhiteSpace(client.LastName)
            ? string.Empty
            : client.LastName[0].ToString();

        return $"{client.FirstName} {initial}".Trim();
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
}