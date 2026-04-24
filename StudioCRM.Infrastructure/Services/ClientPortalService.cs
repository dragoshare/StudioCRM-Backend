using Microsoft.EntityFrameworkCore;
using StudioCRM.Application.DTOs.ClientPortal;
using StudioCRM.Application.Interfaces;
using StudioCRM.Infrastructure.Persistence;

namespace StudioCRM.Infrastructure.Services;

public class ClientPortalService : IClientPortalService
{
    private readonly StudioCRMDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public ClientPortalService(
        StudioCRMDbContext context,
        ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<ClientPortalMeDto?> GetMeAsync()
    {
        return await GetCurrentClientQuery()
            .Select(c => new ClientPortalMeDto
            {
                ClientId = c.Id,
                FullName = c.FirstName + " " + c.LastName,
                Email = c.Email,
                PhoneNumber = c.PhoneNumber,
                Goal = c.Goal,
                ProgressPercent = c.ProgressPercent,
                Status = c.Status,
                BillingStatus = c.BillingStatus,
                LocationName = c.Location.Name,
                TrainerFullName = c.Trainer != null
                    ? c.Trainer.User.FirstName + " " + c.Trainer.User.LastName
                    : null
            })
            .FirstOrDefaultAsync();
    }

    public async Task<ClientPortalDashboardDto?> GetDashboardAsync()
    {
        var client = await GetCurrentClientQuery()
            .Include(c => c.ActivePackage)
            .FirstOrDefaultAsync();

        if (client is null)
            return null;

        var now = DateTime.UtcNow;

        var me = await GetMeAsync() ?? new ClientPortalMeDto();
        var package = await GetPackageAsync() ?? new ClientPortalPackageDto();
        var payment = await GetPaymentAsync() ?? new ClientPortalPaymentDto();
        var trainer = await GetTrainerAsync();

        var nextSession = await _context.Sessions
            .Include(s => s.Trainer).ThenInclude(t => t.User)
            .Include(s => s.Location)
            .Where(s => s.ClientId == client.Id && s.StartAt >= now)
            .OrderBy(s => s.StartAt)
            .Select(s => new ClientPortalSessionDto
            {
                Id = s.Id,
                Title = s.Title,
                Note = s.Note,
                StartAt = s.StartAt,
                EndAt = s.EndAt,
                StudioRoom = s.StudioRoom,
                LocationName = s.Location.Name,
                TrainerFullName = s.Trainer.User.FirstName + " " + s.Trainer.User.LastName,
                Status = s.Status
            })
            .FirstOrDefaultAsync();

        var upcomingSessions = await _context.Sessions
            .Include(s => s.Trainer).ThenInclude(t => t.User)
            .Include(s => s.Location)
            .Where(s => s.ClientId == client.Id && s.StartAt >= now)
            .OrderBy(s => s.StartAt)
            .Take(5)
            .Select(s => new ClientPortalSessionDto
            {
                Id = s.Id,
                Title = s.Title,
                Note = s.Note,
                StartAt = s.StartAt,
                EndAt = s.EndAt,
                StudioRoom = s.StudioRoom,
                LocationName = s.Location.Name,
                TrainerFullName = s.Trainer.User.FirstName + " " + s.Trainer.User.LastName,
                Status = s.Status
            })
            .ToListAsync();

        var recentSessions = await _context.Sessions
            .Include(s => s.Trainer).ThenInclude(t => t.User)
            .Include(s => s.Location)
            .Where(s => s.ClientId == client.Id && s.StartAt < now)
            .OrderByDescending(s => s.StartAt)
            .Take(5)
            .Select(s => new ClientPortalSessionDto
            {
                Id = s.Id,
                Title = s.Title,
                Note = s.Note,
                StartAt = s.StartAt,
                EndAt = s.EndAt,
                StudioRoom = s.StudioRoom,
                LocationName = s.Location.Name,
                TrainerFullName = s.Trainer.User.FirstName + " " + s.Trainer.User.LastName,
                Status = s.Status
            })
            .ToListAsync();

        return new ClientPortalDashboardDto
        {
            GreetingName = client.FirstName,
            GreetingMessage = BuildGreetingMessage(client.FirstName),
            Me = me,
            NextSession = nextSession,
            Trainer = trainer,
            Package = package,
            Payment = payment,
            UpcomingSessions = upcomingSessions,
            RecentSessions = recentSessions
        };
    }

    public async Task<List<ClientPortalSessionDto>> GetScheduleAsync()
    {
        var clientId = await GetCurrentClientIdAsync();

        if (clientId is null)
            return new List<ClientPortalSessionDto>();

        return await _context.Sessions
            .Include(s => s.Trainer).ThenInclude(t => t.User)
            .Include(s => s.Location)
            .Where(s => s.ClientId == clientId.Value)
            .OrderBy(s => s.StartAt)
            .Select(s => new ClientPortalSessionDto
            {
                Id = s.Id,
                Title = s.Title,
                Note = s.Note,
                StartAt = s.StartAt,
                EndAt = s.EndAt,
                StudioRoom = s.StudioRoom,
                LocationName = s.Location.Name,
                TrainerFullName = s.Trainer.User.FirstName + " " + s.Trainer.User.LastName,
                Status = s.Status
            })
            .ToListAsync();
    }

    public async Task<ClientPortalPackageDto?> GetPackageAsync()
    {
        var client = await GetCurrentClientQuery()
            .Include(c => c.ActivePackage)
            .FirstOrDefaultAsync();

        if (client is null)
            return null;

        if (client.ActivePackage is null)
        {
            return new ClientPortalPackageDto
            {
                PackageId = null,
                Name = null,
                UsedSessionsCount = 0,
                RemainingSessionsCount = 0,
                ProgressPercent = 0
            };
        }

        var usedSessionsCount = await _context.Sessions
            .CountAsync(s =>
                s.ClientId == client.Id &&
                s.PackageId == client.ActivePackage.Id &&
                s.Status == "Completed");

        var sessionsLimit = client.ActivePackage.SessionsLimit;
        var remainingSessions = Math.Max(0, sessionsLimit - usedSessionsCount);

        var progressPercent = sessionsLimit > 0
            ? (int)Math.Round((double)usedSessionsCount / sessionsLimit * 100)
            : 0;

        return new ClientPortalPackageDto
        {
            PackageId = client.ActivePackage.Id,
            Name = client.ActivePackage.Name,
            Description = client.ActivePackage.Description,
            Price = client.ActivePackage.Price,
            Currency = client.ActivePackage.Currency,
            SessionsLimit = client.ActivePackage.SessionsLimit,
            UsedSessionsCount = usedSessionsCount,
            RemainingSessionsCount = remainingSessions,
            ProgressPercent = progressPercent,
            DurationDays = client.ActivePackage.DurationDays
        };
    }

    public async Task<ClientPortalPaymentDto?> GetPaymentAsync()
    {
        var client = await GetCurrentClientQuery()
            .Include(c => c.ActivePackage)
            .FirstOrDefaultAsync();

        if (client is null)
            return null;

        var amountDue = client.BillingStatus == "Paid"
            ? 0
            : client.ActivePackage?.Price ?? 0;

        return new ClientPortalPaymentDto
        {
            AmountDue = amountDue,
            Currency = client.ActivePackage?.Currency ?? "PLN",
            BillingStatus = client.BillingStatus,
            PaymentDueDate = client.BillingStatus == "Paid"
                ? null
                : DateTime.UtcNow.Date.AddDays(7)
        };
    }

    public async Task<ClientPortalTrainerDto?> GetTrainerAsync()
    {
        var client = await GetCurrentClientQuery()
            .Include(c => c.Trainer)
                .ThenInclude(t => t!.User)
            .FirstOrDefaultAsync();

        if (client?.Trainer is null)
            return null;

        return new ClientPortalTrainerDto
        {
            TrainerId = client.Trainer.Id,
            FullName = client.Trainer.User.FirstName + " " + client.Trainer.User.LastName,
            Email = client.Trainer.User.Email,
            Phone = client.Trainer.Phone,
            Bio = client.Trainer.Bio,
            AvatarUrl = client.Trainer.AvatarUrl,
            Specialization = client.Trainer.Bio
        };
    }

    private IQueryable<StudioCRM.Domain.Entities.Client> GetCurrentClientQuery()
    {
        if (!_currentUser.UserId.HasValue)
            return _context.Clients.Where(c => false);

        return _context.Clients
            .Include(c => c.Location)
            .Include(c => c.Trainer)
                .ThenInclude(t => t!.User)
            .Where(c => c.UserId == _currentUser.UserId.Value);
    }

    private async Task<int?> GetCurrentClientIdAsync()
    {
        if (!_currentUser.UserId.HasValue)
            return null;

        return await _context.Clients
            .Where(c => c.UserId == _currentUser.UserId.Value)
            .Select(c => (int?)c.Id)
            .FirstOrDefaultAsync();
    }

    private static string BuildGreetingMessage(string firstName)
    {
        var dayName = DateTime.UtcNow.DayOfWeek switch
        {
            DayOfWeek.Monday => "poniedziałek",
            DayOfWeek.Tuesday => "wtorek",
            DayOfWeek.Wednesday => "środa",
            DayOfWeek.Thursday => "czwartek",
            DayOfWeek.Friday => "piątek",
            DayOfWeek.Saturday => "sobota",
            DayOfWeek.Sunday => "niedziela",
            _ => "dzień"
        };

        return $"Dziś jest {dayName}, życzymy udanego treningu!";
    }
}