using Microsoft.EntityFrameworkCore;
using StudioCRM.Application.DTOs.Clients;
using StudioCRM.Application.DTOs.Profiles;
using StudioCRM.Application.DTOs.Sessions;
using StudioCRM.Application.DTOs.TrainerPortal;
using StudioCRM.Application.DTOs.TrainerSettlements;
using StudioCRM.Application.Interfaces;
using StudioCRM.Infrastructure.Persistence;

namespace StudioCRM.Infrastructure.Services;

public class TrainerPortalService : ITrainerPortalService
{
    private readonly StudioCRMDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly IClientService _clientService;
    private readonly ISessionService _sessionService;
    private readonly ITrainerSettlementService _settlementService;

    public TrainerPortalService(
        StudioCRMDbContext context,
        ICurrentUserService currentUser,
        IClientService clientService,
        ISessionService sessionService,
        ITrainerSettlementService settlementService)
    {
        _context = context;
        _currentUser = currentUser;
        _clientService = clientService;
        _sessionService = sessionService;
        _settlementService = settlementService;
    }

    public async Task<TrainerPortalMeDto?> GetMeAsync()
    {
        return await GetCurrentTrainerQuery()
            .Select(t => new TrainerPortalMeDto
            {
                TrainerId = t.Id,
                UserId = t.UserId,
                FullName = t.User.FirstName + " " + t.User.LastName,
                Email = t.User.Email,
                Phone = t.Phone,
                AvatarUrl = t.User.AvatarUrl,
                Bio = t.Bio,
                Status = t.Status,
                TeamJoinedDate = t.TeamJoinedDate,
                LocationIds = t.TrainerLocations.Select(tl => tl.LocationId).ToList(),
                LocationNames = t.TrainerLocations.Select(tl => tl.Location.Name).ToList()
            })
            .FirstOrDefaultAsync();
    }

    public async Task<TrainerPortalMeDto?> UpdateMeAsync(UpdateTrainerPortalProfileRequest request)
    {
        var trainer = await GetCurrentTrainerQuery().FirstOrDefaultAsync();

        if (trainer is null)
            return null;

        if (string.IsNullOrWhiteSpace(request.FirstName) || string.IsNullOrWhiteSpace(request.LastName))
            throw new InvalidOperationException("First name and last name are required.");

        trainer.User.FirstName = request.FirstName.Trim();
        trainer.User.LastName = request.LastName.Trim();
        trainer.User.AvatarUrl = request.AvatarUrl;
        trainer.User.UpdatedAt = DateTime.UtcNow;
        trainer.Phone = request.Phone;
        trainer.Bio = request.Bio;
        trainer.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();
        return await GetMeAsync();
    }

    public async Task<List<TrainerPortalClientDto>> GetClientsAsync()
    {
        var trainerId = await GetCurrentTrainerIdAsync();
        if (trainerId is null)
            return new List<TrainerPortalClientDto>();

        return await _context.Clients
            .Include(c => c.Location)
            .Where(c => c.TrainerId == trainerId.Value)
            .OrderByDescending(c => c.CreatedAt)
            .Select(c => new TrainerPortalClientDto
            {
                ClientId = c.Id,
                FullName = c.FirstName + " " + c.LastName,
                Email = c.Email,
                EmailContactUrl = "mailto:" + c.Email,
                PhoneNumber = c.PhoneNumber,
                PhoneContactUrl = c.PhoneNumber != null ? "tel:" + c.PhoneNumber : null,
                AvatarUrl = c.User != null ? c.User.AvatarUrl : null,
                Goal = c.Goal,
                Status = c.Status,
                BillingStatus = c.BillingStatus,
                LocationName = c.Location.Name,
                CreatedAt = c.CreatedAt
            })
            .ToListAsync();
    }

    public async Task<ClientDto?> GetClientAsync(int clientId)
    {
        var trainerId = await GetCurrentTrainerIdAsync();
        if (trainerId is null)
            return null;

        return await BuildTrainerClientQuery(trainerId.Value)
            .FirstOrDefaultAsync(c => c.Id == clientId);
    }

    public async Task<ClientWorkspaceDto?> GetClientWorkspaceAsync(int clientId)
    {
        var trainerId = await GetCurrentTrainerIdAsync();
        if (trainerId is null)
            return null;

        var ownsClient = await _context.Clients
            .AnyAsync(c => c.Id == clientId && c.TrainerId == trainerId.Value);

        if (!ownsClient)
            return null;

        return await _clientService.GetWorkspaceAsync(clientId);
    }

    public async Task<ClientDto?> UpdateClientAsync(int clientId, UpdateClientDto request)
    {
        var trainerId = await GetCurrentTrainerIdAsync();
        if (trainerId is null)
            return null;

        var client = await _context.Clients
            .Include(c => c.User)
            .FirstOrDefaultAsync(c => c.Id == clientId && c.TrainerId == trainerId.Value);

        if (client is null)
            return null;

        var locationExists = await _context.Locations.AnyAsync(l => l.Id == request.LocationId);
        if (!locationExists)
            throw new InvalidOperationException("Location does not exist.");

        client.FirstName = request.FirstName;
        client.LastName = request.LastName;
        client.Email = request.Email;
        client.PhoneNumber = request.PhoneNumber;
        client.Goal = request.Goal;
        client.Notes = request.Notes;
        client.BillingStatus = request.BillingStatus;
        client.Status = request.Status;
        client.LocationId = request.LocationId;
        client.NextSessionAt = NormalizeNullableDateTime(request.NextSessionAt);
        client.UpdatedAt = DateTime.UtcNow;

        if (client.User is not null)
        {
            client.User.FirstName = client.FirstName;
            client.User.LastName = client.LastName;
            client.User.AvatarUrl = request.AvatarUrl;
            client.User.UpdatedAt = DateTime.UtcNow;
        }

        await _context.SaveChangesAsync();

        return await BuildTrainerClientQuery(trainerId.Value)
            .FirstOrDefaultAsync(c => c.Id == clientId);
    }

    public async Task<bool> DeactivateClientAsync(int clientId)
    {
        var trainerId = await GetCurrentTrainerIdAsync();
        if (trainerId is null)
            return false;

        var client = await _context.Clients
            .FirstOrDefaultAsync(c => c.Id == clientId && c.TrainerId == trainerId.Value);

        if (client is null)
            return false;

        client.IsDeleted = true;
        client.DeletedAt = DateTime.UtcNow;
        client.Status = "Inactive";
        client.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<List<TrainerPortalSessionDto>> GetSessionsAsync()
    {
        var trainerId = await GetCurrentTrainerIdAsync();
        if (trainerId is null)
            return new List<TrainerPortalSessionDto>();

        var locationIds = await _context.TrainerLocations
            .Where(tl => tl.TrainerId == trainerId.Value)
            .Select(tl => tl.LocationId)
            .ToListAsync();

        if (locationIds.Count == 0)
            return new List<TrainerPortalSessionDto>();

        var sessions = await _context.Sessions
            .Include(s => s.Trainer)
                .ThenInclude(t => t.User)
            .Include(s => s.Participants)
            .ThenInclude(p => p.Client)
            .Include(s => s.Location)
            .Where(s => locationIds.Contains(s.LocationId))
            .OrderBy(s => s.StartAt)
            .ToListAsync();

        return sessions
            .Select(s => new TrainerPortalSessionDto
            {
                SessionId = s.Id,
                Title = s.Title,
                Note = s.Note,
                StartAt = ToStudioDisplayDateTime(s.StartAt),
                EndAt = ToStudioDisplayDateTime(s.EndAt),
                TrainerId = s.TrainerId,
                TrainerFullName = s.Trainer.User.FirstName + " " + s.Trainer.User.LastName,
                CanEdit = _currentUser.IsOwner || s.TrainerId == trainerId.Value,
                ClientFullName = string.Join(" + ", s.Participants.Select(p => p.Client.FirstName + " " + p.Client.LastName)),
                LocationName = s.Location.Name,
                Status = s.Status
            })
            .ToList();
    }

    public async Task<SessionDto?> GetSessionAsync(int sessionId)
    {
        var trainerId = await GetCurrentTrainerIdAsync();
        if (trainerId is null)
            return null;

        var canViewSession = await TrainerCanViewSessionAsync(trainerId.Value, sessionId);

        if (!canViewSession)
            return null;

        return await _sessionService.GetByIdAsync(sessionId);
    }

    public async Task<SessionWorkspaceDto?> GetSessionWorkspaceAsync(int sessionId)
    {
        var trainerId = await GetCurrentTrainerIdAsync();
        if (trainerId is null)
            return null;

        var canViewSession = await TrainerCanViewSessionAsync(trainerId.Value, sessionId);

        if (!canViewSession)
            return null;

        return await _sessionService.GetWorkspaceAsync(sessionId);
    }

    public async Task<SessionDto> CreateSessionAsync(CreateSessionDto request)
    {
        var trainerId = await GetCurrentTrainerIdAsync()
            ?? throw new InvalidOperationException("Trainer not found.");

        await ValidateTrainerCanManageSessionAsync(trainerId, request.LocationId, request.Participants);

        request.TrainerId = trainerId;
        return await _sessionService.CreateAsync(request);
    }

    public async Task<SessionDto?> UpdateSessionAsync(int sessionId, UpdateSessionDto request)
    {
        var trainerId = await GetCurrentTrainerIdAsync();
        if (trainerId is null)
            return null;

        var ownsSession = await _context.Sessions
            .AnyAsync(s => s.Id == sessionId && s.TrainerId == trainerId.Value);

        if (!ownsSession)
            return null;

        await ValidateTrainerCanManageSessionAsync(trainerId.Value, request.LocationId, request.Participants);

        request.TrainerId = trainerId.Value;
        return await _sessionService.UpdateAsync(sessionId, request);
    }

    public async Task<TrainerPortalDashboardDto?> GetDashboardAsync()
    {
        var me = await GetMeAsync();
        if (me is null)
            return null;

        var trainerId = await GetCurrentTrainerIdAsync();
        if (trainerId is null)
            return null;

        var today = DateTime.UtcNow.Date;
        var tomorrow = today.AddDays(1);
        var now = DateTime.UtcNow;

        var sessionsQuery = _context.Sessions
            .Include(s => s.Participants)
            .ThenInclude(p => p.Client)
            .Include(s => s.Location)
            .Where(s => s.TrainerId == trainerId.Value);

        var clientsQuery = _context.Clients
            .Include(c => c.Location)
            .Where(c => c.TrainerId == trainerId.Value);

        var activeClientsCount = await clientsQuery.CountAsync(c => c.Status == "Active");
        var todaySessionsCount = await sessionsQuery.CountAsync(s => s.StartAt >= today && s.StartAt < tomorrow);
        var upcomingSessionsCount = await sessionsQuery.CountAsync(s => s.StartAt >= now);

        var todaySessionsSource = await sessionsQuery
            .Where(s => s.StartAt >= today && s.StartAt < tomorrow)
            .OrderBy(s => s.StartAt)
            .Take(8)
            .ToListAsync();

        var todaySessions = todaySessionsSource
            .Select(s => new TrainerPortalSessionDto
            {
                SessionId = s.Id,
                Title = s.Title,
                Note = s.Note,
                StartAt = ToStudioDisplayDateTime(s.StartAt),
                EndAt = ToStudioDisplayDateTime(s.EndAt),
                TrainerId = s.TrainerId,
                TrainerFullName = me.FullName,
                CanEdit = true,
                ClientFullName = string.Join(" + ", s.Participants.Select(p => p.Client.FirstName + " " + p.Client.LastName)),
                LocationName = s.Location.Name,
                Status = s.Status
            })
            .ToList();

        var upcomingSessionsSource = await sessionsQuery
            .Where(s => s.StartAt >= now)
            .OrderBy(s => s.StartAt)
            .Take(8)
            .ToListAsync();

        var upcomingSessions = upcomingSessionsSource
            .Select(s => new TrainerPortalSessionDto
            {
                SessionId = s.Id,
                Title = s.Title,
                Note = s.Note,
                StartAt = ToStudioDisplayDateTime(s.StartAt),
                EndAt = ToStudioDisplayDateTime(s.EndAt),
                TrainerId = s.TrainerId,
                TrainerFullName = me.FullName,
                CanEdit = true,
                ClientFullName = string.Join(" + ", s.Participants.Select(p => p.Client.FirstName + " " + p.Client.LastName)),
                LocationName = s.Location.Name,
                Status = s.Status
            })
            .ToList();

        var recentClients = await clientsQuery
            .OrderByDescending(c => c.CreatedAt)
            .Take(8)
            .Select(c => new TrainerPortalClientDto
            {
                ClientId = c.Id,
                FullName = c.FirstName + " " + c.LastName,
                Email = c.Email,
                EmailContactUrl = "mailto:" + c.Email,
                PhoneNumber = c.PhoneNumber,
                PhoneContactUrl = c.PhoneNumber != null ? "tel:" + c.PhoneNumber : null,
                AvatarUrl = c.User != null ? c.User.AvatarUrl : null,
                Goal = c.Goal,
                Status = c.Status,
                BillingStatus = c.BillingStatus,
                LocationName = c.Location.Name,
                CreatedAt = c.CreatedAt
            })
            .ToListAsync();

        return new TrainerPortalDashboardDto
        {
            Me = me,
            ActiveClientsCount = activeClientsCount,
            TodaySessionsCount = todaySessionsCount,
            UpcomingSessionsCount = upcomingSessionsCount,
            TodaySessions = todaySessions,
            UpcomingSessions = upcomingSessions,
            RecentClients = recentClients
        };
    }

    public async Task<TrainerMonthlySettlementDto?> GetMyMonthlySettlementAsync(int year, int month)
    {
        if (!_currentUser.IsTrainer && !_currentUser.IsOwner)
            throw new UnauthorizedAccessException("Only trainer can access this endpoint.");

        var trainerId = await _context.Trainers
            .Where(t => t.UserId == _currentUser.UserId)
            .Select(t => t.Id)
            .FirstOrDefaultAsync();

        if (trainerId == 0)
            throw new InvalidOperationException("Trainer not found.");

        return await _settlementService.GetMonthlySettlementAsync(trainerId, year, month);
    }

    private IQueryable<StudioCRM.Domain.Entities.Trainer> GetCurrentTrainerQuery()
    {
        if (!_currentUser.UserId.HasValue)
            return _context.Trainers.Where(t => false);

        return _context.Trainers
            .Include(t => t.User)
            .Include(t => t.TrainerLocations)
                .ThenInclude(tl => tl.Location)
            .Where(t => t.UserId == _currentUser.UserId.Value);
    }

    private async Task<int?> GetCurrentTrainerIdAsync()
    {
        if (!_currentUser.UserId.HasValue)
            return null;

        return await _context.Trainers
            .Where(t => t.UserId == _currentUser.UserId.Value)
            .Select(t => (int?)t.Id)
            .FirstOrDefaultAsync();
    }

    private async Task ValidateTrainerCanManageSessionAsync(
        int trainerId,
        int locationId,
        IReadOnlyCollection<CreateSessionParticipantDto>? participants)
    {
        var hasLocationAccess = await _context.TrainerLocations
            .AnyAsync(tl => tl.TrainerId == trainerId && tl.LocationId == locationId);

        if (!hasLocationAccess)
            throw new InvalidOperationException("Trainer cannot manage sessions in this location.");

        if (participants is null || participants.Count == 0)
            return;

        var clientIds = participants
            .Select(p => p.ClientId)
            .Distinct()
            .ToList();

        var ownedClientIds = await _context.Clients
            .Where(c => c.TrainerId == trainerId && clientIds.Contains(c.Id))
            .Select(c => c.Id)
            .ToListAsync();

        if (ownedClientIds.Count != clientIds.Count)
            throw new InvalidOperationException("Trainer can only add their own clients to sessions.");
    }

    private async Task<bool> TrainerCanViewSessionAsync(int trainerId, int sessionId)
    {
        if (_currentUser.IsOwner)
            return true;

        return await _context.Sessions.AnyAsync(s =>
            s.Id == sessionId &&
            s.Location.TrainerLocations.Any(tl => tl.TrainerId == trainerId));
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

    private static DateTime? NormalizeNullableDateTime(DateTime? value)
    {
        if (!value.HasValue)
            return null;

        return value.Value.Kind switch
        {
            DateTimeKind.Utc => value.Value,
            DateTimeKind.Local => value.Value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value.Value, DateTimeKind.Utc)
        };
    }

    private IQueryable<ClientDto> BuildTrainerClientQuery(int trainerId)
    {
        return _context.Clients
            .Include(c => c.Trainer)
                .ThenInclude(t => t!.User)
            .Include(c => c.Location)
            .Where(c => c.TrainerId == trainerId)
            .Select(c => new ClientDto
            {
                Id = c.Id,
                TrainerId = c.TrainerId,
                ActivePackageId = c.ActivePackageId,
                LocationId = c.LocationId,
                LocationName = c.Location.Name,
                FirstName = c.FirstName,
                LastName = c.LastName,
                FullName = c.FirstName + " " + c.LastName,
                Email = c.Email,
                EmailContactUrl = "mailto:" + c.Email,
                PhoneNumber = c.PhoneNumber,
                PhoneContactUrl = c.PhoneNumber != null ? "tel:" + c.PhoneNumber : null,
                AvatarUrl = c.User != null ? c.User.AvatarUrl : null,
                Goal = c.Goal,
                Notes = c.Notes,
                BillingStatus = c.BillingStatus,
                Status = c.Status,
                NextSessionAt = c.NextSessionAt,
                CreatedAt = c.CreatedAt,
                UpdatedAt = c.UpdatedAt,
                CreatedBy = c.CreatedBy,
                TrainerFullName = c.Trainer != null
                    ? c.Trainer.User.FirstName + " " + c.Trainer.User.LastName
                    : null
            });
    }
}
