using Microsoft.EntityFrameworkCore;
using StudioCRM.Application.DTOs.TrainerPortal;
using StudioCRM.Application.DTOs.TrainerSettlements;
using StudioCRM.Application.Interfaces;
using StudioCRM.Infrastructure.Persistence;

namespace StudioCRM.Infrastructure.Services;

public class TrainerPortalService : ITrainerPortalService
{
    private readonly StudioCRMDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly ITrainerSettlementService _settlementService;
    public TrainerPortalService(
    StudioCRMDbContext context,
    ICurrentUserService currentUser,
    ITrainerSettlementService settlementService)
    {
        _context = context;
        _currentUser = currentUser;
        _settlementService = settlementService;
    }

    public async Task<TrainerPortalMeDto?> GetMeAsync()
    {
        var trainer = await GetCurrentTrainerQuery()
            .Select(t => new TrainerPortalMeDto
            {
                TrainerId = t.Id,
                UserId = t.UserId,
                FullName = t.User.FirstName + " " + t.User.LastName,
                Email = t.User.Email,
                Phone = t.Phone,
                Bio = t.Bio,
                Status = t.Status,
                ExperienceYears = t.ExperienceYears,
                LocationIds = t.TrainerLocations.Select(tl => tl.LocationId).ToList(),
                LocationNames = t.TrainerLocations.Select(tl => tl.Location.Name).ToList()
            })
            .FirstOrDefaultAsync();

        return trainer;
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
                PhoneNumber = c.PhoneNumber,
                Status = c.Status,
                BillingStatus = c.BillingStatus,
                ProgressPercent = c.ProgressPercent,
                LocationName = c.Location.Name,
                CreatedAt = c.CreatedAt
            })
            .ToListAsync();
    }

    public async Task<List<TrainerPortalSessionDto>> GetSessionsAsync()
    {
        var trainerId = await GetCurrentTrainerIdAsync();
        if (trainerId is null)
            return new List<TrainerPortalSessionDto>();

        return await _context.Sessions
            .Include(s => s.Participants)
            .ThenInclude(p => p.Client)
            .Include(s => s.Location)
            .Where(s => s.TrainerId == trainerId.Value)
            .OrderBy(s => s.StartAt)
            .Select(s => new TrainerPortalSessionDto
            {
                SessionId = s.Id,
                Title = s.Title,
                Note = s.Note,
                StartAt = s.StartAt,
                EndAt = s.EndAt,
                ClientFullName = string.Join(" + ", s.Participants.Select(p => p.Client.FirstName + " " + p.Client.LastName)),
                LocationName = s.Location.Name,
                StudioRoom = s.StudioRoom,
                Status = s.Status
            })
            .ToListAsync();
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

        var todaySessions = await sessionsQuery
            .Where(s => s.StartAt >= today && s.StartAt < tomorrow)
            .OrderBy(s => s.StartAt)
            .Take(8)
            .Select(s => new TrainerPortalSessionDto
            {
                SessionId = s.Id,
                Title = s.Title,
                Note = s.Note,
                StartAt = s.StartAt,
                EndAt = s.EndAt,
                ClientFullName = string.Join(" + ", s.Participants.Select(p => p.Client.FirstName + " " + p.Client.LastName)),
                LocationName = s.Location.Name,
                StudioRoom = s.StudioRoom,
                Status = s.Status
            })
            .ToListAsync();

        var upcomingSessions = await sessionsQuery
            .Where(s => s.StartAt >= now)
            .OrderBy(s => s.StartAt)
            .Take(8)
            .Select(s => new TrainerPortalSessionDto
            {
                SessionId = s.Id,
                Title = s.Title,
                Note = s.Note,
                StartAt = s.StartAt,
                EndAt = s.EndAt,
                ClientFullName = string.Join(" + ", s.Participants.Select(p => p.Client.FirstName + " " + p.Client.LastName)),
                LocationName = s.Location.Name,
                StudioRoom = s.StudioRoom,
                Status = s.Status
            })
            .ToListAsync();

        var recentClients = await clientsQuery
            .OrderByDescending(c => c.CreatedAt)
            .Take(8)
            .Select(c => new TrainerPortalClientDto
            {
                ClientId = c.Id,
                FullName = c.FirstName + " " + c.LastName,
                Email = c.Email,
                PhoneNumber = c.PhoneNumber,
                Status = c.Status,
                BillingStatus = c.BillingStatus,
                ProgressPercent = c.ProgressPercent,
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
    public async Task<TrainerMonthlySettlementDto?> GetMyMonthlySettlementAsync(int year, int month)
    {
        if (!_currentUser.IsTrainer)
            throw new UnauthorizedAccessException("Only trainer can access this endpoint.");

        var trainerId = await _context.Trainers
            .Where(t => t.UserId == _currentUser.UserId)
            .Select(t => t.Id)
            .FirstOrDefaultAsync();

        if (trainerId == 0)
            throw new InvalidOperationException("Trainer not found.");

        return await _settlementService.GetMonthlySettlementAsync(trainerId, year, month);
    }
}