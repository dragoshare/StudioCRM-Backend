using Microsoft.EntityFrameworkCore;
using StudioCRM.Application.DTOs.ClientPortal;
using StudioCRM.Application.DTOs.Profiles;
using StudioCRM.Application.Interfaces;
using StudioCRM.Domain.Entities;
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
                FirstName = c.FirstName,
                LastName = c.LastName,
                FullName = c.FirstName + " " + c.LastName,
                Email = c.Email,
                PhoneNumber = c.PhoneNumber,
                AvatarUrl = c.User != null ? c.User.AvatarUrl : null,
                Goal = c.Goal,
                Status = c.Status,
                BillingStatus = c.BillingStatus,
                LocationName = c.Location.Name,
                TrainerFullName = c.Trainer != null
                    ? c.Trainer.User.FirstName + " " + c.Trainer.User.LastName
                    : null
            })
            .FirstOrDefaultAsync();
    }

    public async Task<ClientPortalMeDto?> UpdateMeAsync(UpdateClientPortalProfileRequest request)
    {
        var client = await GetCurrentClientQuery()
            .Include(c => c.User)
            .FirstOrDefaultAsync();

        if (client is null)
            return null;

        if (string.IsNullOrWhiteSpace(request.FirstName) || string.IsNullOrWhiteSpace(request.LastName))
            throw new InvalidOperationException("First name and last name are required.");

        client.FirstName = request.FirstName.Trim();
        client.LastName = request.LastName.Trim();
        client.PhoneNumber = request.PhoneNumber;
        client.UpdatedAt = DateTime.UtcNow;

        if (client.User is not null)
        {
            client.User.FirstName = client.FirstName;
            client.User.LastName = client.LastName;
            client.User.AvatarUrl = request.AvatarUrl;
            client.User.UpdatedAt = DateTime.UtcNow;
        }

        await _context.SaveChangesAsync();

        return await GetMeAsync();
    }

    public async Task RequestEmailChangeAsync(RequestEmailChangeDto request)
    {
        if (string.IsNullOrWhiteSpace(request.RequestedEmail))
            throw new InvalidOperationException("Requested email is required.");

        var client = await GetCurrentClientQuery().FirstOrDefaultAsync();
        if (client is null)
            throw new InvalidOperationException("Client profile not found for current user.");

        var requestedEmail = request.RequestedEmail.Trim();
        var emailAlreadyExists = await _context.Users.AnyAsync(u => u.Email == requestedEmail);
        if (emailAlreadyExists)
            throw new InvalidOperationException("User with this email already exists.");

        var existingPending = await _context.ClientEmailChangeRequests.AnyAsync(r =>
            r.ClientId == client.Id &&
            r.Status == "Pending");

        if (existingPending)
            throw new InvalidOperationException("There is already a pending email change request.");

        await _context.ClientEmailChangeRequests.AddAsync(new ClientEmailChangeRequest
        {
            ClientId = client.Id,
            CurrentEmail = client.Email,
            RequestedEmail = requestedEmail,
            Status = "Pending",
            CreatedAt = DateTime.UtcNow,
            CreatedByUserId = _currentUser.UserId
        });

        await _context.SaveChangesAsync();
    }

    public async Task<ClientPortalDashboardDto?> GetDashboardAsync()
    {
        var client = await GetCurrentClientQuery()
            .FirstOrDefaultAsync();

        if (client is null)
            return null;

        var now = DateTime.UtcNow;

        var me = await GetMeAsync() ?? new ClientPortalMeDto();
        var package = await GetPackageAsync() ?? new ClientPortalPackageDto();
        var payment = await GetPaymentAsync() ?? new ClientPortalPaymentDto();
        var trainer = await GetTrainerAsync();

        var nextSessionSource = await _context.Sessions
            .Include(s => s.Trainer).ThenInclude(t => t.User)
            .Include(s => s.Location)
            .Where(s => s.Participants.Any(p => p.ClientId == client.Id) && s.StartAt >= now)
            .OrderBy(s => s.StartAt)
            .FirstOrDefaultAsync();

        var nextSession = nextSessionSource is null
            ? null
            : MapClientSession(nextSessionSource);

        var upcomingSessionsSource = await _context.Sessions
            .Include(s => s.Trainer).ThenInclude(t => t.User)
            .Include(s => s.Location)
            .Where(s => s.Participants.Any(p => p.ClientId == client.Id) && s.StartAt >= now)
            .OrderBy(s => s.StartAt)
            .Take(5)
            .ToListAsync();

        var upcomingSessions = upcomingSessionsSource
            .Select(MapClientSession)
            .ToList();

        var recentSessionsSource = await _context.Sessions
            .Include(s => s.Trainer).ThenInclude(t => t.User)
            .Include(s => s.Location)
            .Where(s => s.Participants.Any(p => p.ClientId == client.Id) && s.StartAt < now)
            .OrderByDescending(s => s.StartAt)
            .Take(5)
            .ToListAsync();

        var recentSessions = recentSessionsSource
            .Select(MapClientSession)
            .ToList();

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

        var sessions = await _context.Sessions
            .Include(s => s.Trainer).ThenInclude(t => t.User)
            .Include(s => s.Location)
            .Where(s => s.Participants.Any(p => p.ClientId == clientId.Value))
            .OrderBy(s => s.StartAt)
            .ToListAsync();

        return sessions
            .Select(MapClientSession)
            .ToList();
    }

    private async Task<ClientPortalPackageDto?> GetPackageAsync()
    {
        var client = await GetCurrentClientQuery()
            .FirstOrDefaultAsync();

        if (client is null)
            return null;

        var activeCycle = await _context.ClientPackages
            .Include(cp => cp.Package)
            .Where(cp => cp.ClientId == client.Id && cp.IsActive)
            .OrderByDescending(cp => cp.ActivatedAt ?? cp.PurchaseDate)
            .FirstOrDefaultAsync();

        if (activeCycle is null)
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

        var usedSessionsCount = activeCycle.UsedSessions;
        var sessionsLimit = activeCycle.TotalSessions;
        var remainingSessions = Math.Max(0, sessionsLimit - usedSessionsCount);

        var progressPercent = sessionsLimit > 0
            ? (int)Math.Round((double)usedSessionsCount / sessionsLimit * 100)
            : 0;

        return new ClientPortalPackageDto
        {
            PackageId = activeCycle.PackageId,
            Name = activeCycle.Name,
            Description = activeCycle.Package?.Description,
            Price = activeCycle.TotalPrice,
            Currency = activeCycle.Currency,
            SessionsLimit = activeCycle.TotalSessions,
            UsedSessionsCount = usedSessionsCount,
            RemainingSessionsCount = remainingSessions,
            ProgressPercent = progressPercent,
            DurationDays = activeCycle.Package?.DurationDays
        };
    }

    private async Task<ClientPortalPaymentDto?> GetPaymentAsync()
    {
        var client = await GetCurrentClientQuery()
            .FirstOrDefaultAsync();

        if (client is null)
            return null;

        var activePackage = await _context.ClientPackages
            .Where(cp => cp.ClientId == client.Id && cp.IsActive)
            .OrderByDescending(cp => cp.PurchaseDate)
            .FirstOrDefaultAsync();

        if (activePackage is null)
        {
            return new ClientPortalPaymentDto
            {
                AmountDue = 0,
                Currency = "PLN",
                BillingStatus = "NoActivePackage"
            };
        }

        return new ClientPortalPaymentDto
        {
            AmountDue = Math.Max(0, activePackage.TotalPrice - activePackage.AmountPaid),
            Currency = activePackage.Currency,
            BillingStatus = activePackage.PaymentStatus.ToString(),
            PaymentDueDate = activePackage.PaymentStatus.ToString() == "Paid"
                ? null
                : activePackage.PaymentDueDate
        };
    }

    private async Task<ClientPortalTrainerDto?> GetTrainerAsync()
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
            EmailContactUrl = "mailto:" + client.Trainer.User.Email,
            Phone = client.Trainer.Phone,
            Bio = client.Trainer.Bio,
            AvatarUrl = client.Trainer.User.AvatarUrl,
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

    private static ClientPortalSessionDto MapClientSession(Session session)
    {
        return new ClientPortalSessionDto
        {
            Id = session.Id,
            Title = session.Title,
            Note = session.Note,
            StartAt = ToStudioDisplayDateTime(session.StartAt),
            EndAt = ToStudioDisplayDateTime(session.EndAt),
            LocationName = session.Location.Name,
            TrainerFullName = session.Trainer.User.FirstName + " " + session.Trainer.User.LastName,
            Status = session.Status
        };
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

    private static string BuildGreetingMessage(string firstName)
    {
        var dayName = ToStudioDisplayDateTime(DateTime.UtcNow).DayOfWeek switch
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
    public async Task<ClientTrainerContactDto?> GetTrainerContactAsync(int userId)
    {
        var client = await _context.Clients
            .Include(c => c.Trainer)
                .ThenInclude(t => t!.User)
            .FirstOrDefaultAsync(c => c.UserId == userId);

        if (client?.Trainer?.User is null)
            return null;

        var trainer = client.Trainer;
        var trainerUser = trainer.User;

        return new ClientTrainerContactDto
        {
            TrainerId = trainer.Id,
            FullName = $"{trainerUser.FirstName} {trainerUser.LastName}".Trim(),
            Email = trainerUser.Email,
            EmailContactUrl = "mailto:" + trainerUser.Email,
            Phone = trainer.Phone,
            PhoneContactUrl = !string.IsNullOrWhiteSpace(trainer.Phone) ? "tel:" + trainer.Phone : null,
            Bio = trainer.Bio,
            AvatarUrl = trainerUser.AvatarUrl,
            ExperienceYears = trainer.ExperienceYears
        };
    }

    public async Task<ClientOwnerContactDto?> GetOwnerContactAsync()
    {
        return await _context.Users
            .Include(u => u.UserRoles)
                .ThenInclude(ur => ur.Role)
            .Where(u => u.IsActive && u.UserRoles.Any(ur => ur.Role.Name == "Owner"))
            .OrderBy(u => u.Id)
            .Select(u => new ClientOwnerContactDto
            {
                UserId = u.Id,
                FullName = (u.FirstName + " " + u.LastName).Trim(),
                Email = u.Email,
                EmailContactUrl = "mailto:" + u.Email,
                AvatarUrl = u.AvatarUrl
            })
            .FirstOrDefaultAsync();
    }
}
