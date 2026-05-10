using Microsoft.EntityFrameworkCore;
using StudioCRM.Application.DTOs.ClientPortal;
using StudioCRM.Application.DTOs.Profiles;
using StudioCRM.Application.Interfaces;
using StudioCRM.Domain.Entities;
using StudioCRM.Domain.Enums;
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
                AvatarUrl = c.AvatarUrl,
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
        client.AvatarUrl = request.AvatarUrl;
        client.UpdatedAt = DateTime.UtcNow;

        if (client.User is not null)
        {
            client.User.FirstName = client.FirstName;
            client.User.LastName = client.LastName;
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
            .Where(s => s.Participants.Any(p => p.ClientId == client.Id) && s.StartAt >= now)
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
            .Where(s => s.Participants.Any(p => p.ClientId == client.Id) && s.StartAt >= now)
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
            .Where(s => s.Participants.Any(p => p.ClientId == client.Id) && s.StartAt < now)
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
            .Where(s => s.Participants.Any(p => p.ClientId == clientId.Value))
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
                s.Participants.Any(p => p.ClientId == client.Id) &&
                s.Participants.Any(p => p.PackageId == client.ActivePackageId) &&
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

    public async Task<ClientPackageSettlementDto> GetPackageSettlementAsync(string userId)
    {
        if (!int.TryParse(userId, out var parsedUserId))
            throw new InvalidOperationException("Invalid user id.");

        var client = await _context.Clients
            .FirstOrDefaultAsync(c => c.UserId == parsedUserId);

        if (client is null)
            throw new InvalidOperationException("Client profile not found for current user.");

        var activePackage = await _context.ClientPackages
            .Include(cp => cp.Package)
            .Include(cp => cp.Location)
            .Where(cp => cp.ClientId == client.Id && cp.IsActive)
            .OrderByDescending(cp => cp.PurchaseDate)
            .FirstOrDefaultAsync();

        var balanceTransactions = await _context.ClientBalanceTransactions
            .Where(t => t.ClientId == client.Id)
            .OrderByDescending(t => t.CreatedAt)
            .Select(t => new ClientBalanceTransactionDto
            {
                Id = t.Id,
                Amount = t.Amount,
                Type = t.Type.ToString(),
                Description = t.Description,
                CreatedAt = t.CreatedAt
            })
            .ToListAsync();

        var currentBalance = balanceTransactions
            .Where(t => t.Type != BalanceTransactionType.PaymentCredit.ToString())
            .Sum(t => t.Amount);

        if (activePackage is null)
        {
            return new ClientPackageSettlementDto
            {
                ClientId = client.Id,
                ClientName = client.FirstName + " " + client.LastName,
                ActivePackage = null,
                CurrentBalance = currentBalance,
                CountedSessions = new List<ClientCountedSessionDto>(),
                BalanceTransactions = balanceTransactions
            };
        }

        var countedSessions = await _context.SessionParticipants
            .Include(sp => sp.Session)
                .ThenInclude(s => s.Trainer)
                    .ThenInclude(t => t.User)
            .Include(sp => sp.Session)
                .ThenInclude(s => s.Location)
            .Where(sp =>
                sp.ClientId == client.Id &&
                sp.ClientPackageId == activePackage.Id &&
                sp.CountsAgainstPackage)
            .OrderByDescending(sp => sp.Session.StartAt)
            .Select(sp => new ClientCountedSessionDto
            {
                SessionId = sp.SessionId,
                Date = sp.Session.StartAt,
                TrainerName = sp.Session.Trainer.User.FirstName + " " + sp.Session.Trainer.User.LastName,
                LocationName = sp.Session.Location.Name,
                Status = sp.Session.Status,
                WasCountedFromPackage = sp.CountsAgainstPackage,
                PlannedBillingType = sp.PlannedBillingType != null ? sp.PlannedBillingType.ToString()! : string.Empty,
                ActualBillingType = sp.ActualBillingType != null ? sp.ActualBillingType.ToString()! : string.Empty,
                ExpectedUnitPrice = sp.ExpectedUnitPrice ?? 0,
                ActualUnitPrice = sp.ActualUnitPrice ?? 0,
                BalanceDifference = sp.BalanceDifference ?? 0,
                Description = sp.BalanceDifference > 0
                    ? "Sesja rozliczona taniej niż zakładany typ pakietu."
                    : sp.BalanceDifference < 0
                        ? "Sesja rozliczona drożej niż zakładany typ pakietu."
                        : "Sesja rozliczona zgodnie z pakietem."
            })
            .ToListAsync();

        var usedSessions = countedSessions.Count;
        var remainingSessions = Math.Max(0, activePackage.TotalSessions - usedSessions);

        return new ClientPackageSettlementDto
        {
            ClientId = client.Id,
            ClientName = client.FirstName + " " + client.LastName,
            CurrentBalance = currentBalance,
            BalanceTransactions = balanceTransactions,
            CountedSessions = countedSessions,
            ActivePackage = new ClientActivePackageDto
            {
                ClientPackageId = activePackage.Id,
                PackageId = activePackage.PackageId,
                PackageName = activePackage.Name,
                TotalSessions = activePackage.TotalSessions,
                SessionsPerWeek = activePackage.SessionsPerWeek,
                UsedSessions = usedSessions,
                RemainingSessions = remainingSessions,
                PackagePrice = activePackage.TotalPrice,
                ExpectedUnitPrice = activePackage.ExpectedUnitPrice,
                ExpectedBillingType = activePackage.ExpectedBillingType.ToString(),
                LocationId = activePackage.LocationId,
                LocationName = activePackage.Location != null ? activePackage.Location.Name : null,
                PaymentStatus = activePackage.PaymentStatus.ToString(),
                IsPaid = activePackage.PaymentStatus.ToString() == "Paid",
                IsOverdue =
                    activePackage.PaymentStatus.ToString() == "Overdue" ||
                    activePackage.PaymentDueDate < DateTime.UtcNow &&
                    activePackage.PaymentStatus.ToString() != "Paid",
                PurchaseDate = activePackage.PurchaseDate,
                ValidUntil = activePackage.ValidUntil
            }
        };
    }

    public async Task<ClientPortalPaymentDto?> GetPaymentAsync()
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
            Phone = trainer.Phone,
            Bio = trainer.Bio,
            AvatarUrl = trainer.AvatarUrl,
            ExperienceYears = trainer.ExperienceYears
        };
    }
}
