using Microsoft.EntityFrameworkCore;
using StudioCRM.Application.DTOs.Billing;
using StudioCRM.Application.DTOs.Clients;
using StudioCRM.Application.DTOs.Subscriptions;
using StudioCRM.Application.DTOs.TrainingPlans;
using StudioCRM.Application.Interfaces;
using StudioCRM.Domain.Entities;
using StudioCRM.Infrastructure.Persistence;

namespace StudioCRM.Infrastructure.Services;

public class ClientService : IClientService
{
    private readonly StudioCRMDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly IClientPaymentService _clientPaymentService;
    private readonly ISubscriptionService _subscriptionService;

    public ClientService(
        StudioCRMDbContext context,
        ICurrentUserService currentUser,
        IClientPaymentService clientPaymentService,
        ISubscriptionService subscriptionService)
    {
        _context = context;
        _currentUser = currentUser;
        _clientPaymentService = clientPaymentService;
        _subscriptionService = subscriptionService;
    }

    public async Task<ClientDto> CreateAsync(CreateClientDto request)
    {
        if (request.TrainerId.HasValue)
        {
            var trainerExists = await _context.Trainers.AnyAsync(t => t.Id == request.TrainerId.Value);
            if (!trainerExists)
                throw new InvalidOperationException("Trainer does not exist.");
        }

        var locationExists = await _context.Locations.AnyAsync(l => l.Id == request.LocationId);
        if (!locationExists)
            throw new InvalidOperationException("Location does not exist.");

        if (_currentUser.IsTrainer && !_currentUser.IsOwner)
        {
            if (!_currentUser.UserId.HasValue)
                throw new InvalidOperationException("Current trainer user is invalid.");

            var currentTrainer = await _context.Trainers
                .FirstOrDefaultAsync(t => t.UserId == _currentUser.UserId.Value);

            if (currentTrainer is null)
                throw new InvalidOperationException("Trainer profile not found.");

            if (request.TrainerId.HasValue && request.TrainerId.Value != currentTrainer.Id)
                throw new InvalidOperationException("Trainer can create clients only for themselves.");

            request.TrainerId = currentTrainer.Id;
        }

        var client = new Client
        {
            TrainerId = request.TrainerId,
            LocationId = request.LocationId,
            FirstName = request.FirstName,
            LastName = request.LastName,
            Email = request.Email,
            PhoneNumber = request.PhoneNumber,
            Goal = request.Goal,
            Notes = request.Notes,
            BillingStatus = request.BillingStatus ?? "Pending",
            Status = "Inactive",
            NextSessionAt = NormalizeNullableDateTime(request.NextSessionAt),
            TrainingStartDate = NormalizeNullableDate(request.TrainingStartDate),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            CreatedBy = request.CreatedBy
        };

        await _context.Clients.AddAsync(client);
        await _context.SaveChangesAsync();

        return await GetProjectedById(client.Id);
    }

    public async Task<List<ClientDto>> GetAllAsync()
    {
        var query = ApplyAccessControl(BuildClientQuery());

        return await query
            .OrderByDescending(c => c.CreatedAt)
            .ToListAsync();
    }

    public async Task<ClientDto?> GetByIdAsync(int id)
    {
        var query = ApplyAccessControl(BuildClientQuery());

        return await query
            .FirstOrDefaultAsync(c => c.Id == id);
    }

    public async Task<ClientWorkspaceDto?> GetWorkspaceAsync(int id)
    {
        var profile = await GetByIdAsync(id);

        if (profile is null)
            return null;

        var client = await _context.Clients
            .Include(c => c.Trainer)
                .ThenInclude(t => t!.User)
            .Include(c => c.Location)
            .FirstOrDefaultAsync(c => c.Id == id);

        if (client is null)
            return null;

        var subscription = await TryLoadAsync(() => _subscriptionService.GetClientSubscriptionAsync(id));
        var billing = await TryLoadAsync(() => _clientPaymentService.GetClientSummaryAsync(id));
        var trainingPlan = await TryLoadAsync(() => _subscriptionService.GetClientTrainingPlanAsync(id));

        return new ClientWorkspaceDto
        {
            Profile = profile,
            Trainer = client.Trainer is null
                ? null
                : new ClientWorkspaceTrainerDto
                {
                    TrainerId = client.Trainer.Id,
                    FullName = client.Trainer.User.FirstName + " " + client.Trainer.User.LastName,
                    Email = client.Trainer.User.Email,
                    EmailContactUrl = "mailto:" + client.Trainer.User.Email,
                    Phone = client.Trainer.Phone,
                    PhoneContactUrl = !string.IsNullOrWhiteSpace(client.Trainer.Phone)
                        ? "tel:" + client.Trainer.Phone
                        : null,
                    AvatarUrl = client.Trainer.User.AvatarUrl
                },
            Subscription = subscription,
            Billing = billing,
            TrainingPlan = trainingPlan,
            UpcomingSessions = await BuildUpcomingSessionsAsync(id),
            CountedSessions = await BuildCountedSessionsAsync(id),
            QuickActions = new ClientWorkspaceQuickActionsDto
            {
                CanDeactivate = !client.IsDeleted,
                GoogleDriveFolderUrl = trainingPlan?.GoogleDriveFolderUrl,
                TrainingPlanUrl = trainingPlan?.Url
            }
        };
    }

    public async Task<ClientDto?> UpdateAsync(int id, UpdateClientDto request)
    {
        if (request.TrainerId.HasValue)
        {
            var trainerExists = await _context.Trainers.AnyAsync(t => t.Id == request.TrainerId.Value);
            if (!trainerExists)
                throw new InvalidOperationException("Trainer does not exist.");
        }

        var locationExists = await _context.Locations.AnyAsync(l => l.Id == request.LocationId);
        if (!locationExists)
            throw new InvalidOperationException("Location does not exist.");

        var client = await _context.Clients.FirstOrDefaultAsync(c => c.Id == id);
        if (client is null)
            return null;

        await EnsureActivePackageMatchesLocationAsync(client.Id, request.LocationId);

        if (_currentUser.IsTrainer && !_currentUser.IsOwner)
        {
            if (!_currentUser.UserId.HasValue)
                throw new InvalidOperationException("Current trainer user is invalid.");

            var currentTrainer = await _context.Trainers
                .FirstOrDefaultAsync(t => t.UserId == _currentUser.UserId.Value);

            if (currentTrainer is null)
                throw new InvalidOperationException("Trainer profile not found.");

            if (client.TrainerId != currentTrainer.Id)
                throw new InvalidOperationException("Trainer can update only their own clients.");

            request.TrainerId = currentTrainer.Id;
        }

        client.TrainerId = request.TrainerId;
        client.LocationId = request.LocationId;
        client.FirstName = request.FirstName;
        client.LastName = request.LastName;
        client.Email = request.Email;
        client.PhoneNumber = request.PhoneNumber;
        client.Goal = request.Goal;
        client.Notes = request.Notes;
        client.BillingStatus = request.BillingStatus;
        client.Status = await ResolveClientStatusAsync(client.Id);
        client.NextSessionAt = NormalizeNullableDateTime(request.NextSessionAt);
        client.TrainingStartDate = NormalizeNullableDate(request.TrainingStartDate);
        client.UpdatedAt = DateTime.UtcNow;

        if (client.User is not null)
        {
            client.User.FirstName = client.FirstName;
            client.User.LastName = client.LastName;
            client.User.UpdatedAt = DateTime.UtcNow;
        }

        await _context.SaveChangesAsync();

        return await GetProjectedById(id);
    }

    public async Task<bool> DeleteAsync(int id)
    {
        var client = await _context.Clients.FirstOrDefaultAsync(c => c.Id == id);
        if (client is null)
            return false;

        client.IsDeleted = true;
        client.DeletedAt = DateTime.UtcNow;
        client.Status = "Inactive";
        client.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> AssignTrainerAsync(int id, SetClientTrainerRequest request)
    {
        if (!_currentUser.IsOwner)
            throw new InvalidOperationException("Only owner can assign trainers.");

        var client = await _context.Clients
            .Include(c => c.User)
            .FirstOrDefaultAsync(c => c.Id == id);
        if (client is null)
            return false;

        if (request.TrainerId.HasValue)
        {
            var trainerExists = await _context.Trainers
                .AnyAsync(t => t.Id == request.TrainerId.Value && !t.IsDeleted);

            if (!trainerExists)
                throw new InvalidOperationException("Trainer does not exist.");
        }

        client.TrainerId = request.TrainerId;
        client.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> RestoreAsync(int id)
    {
        var client = await _context.Clients
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(c => c.Id == id);

        if (client is null || !client.IsDeleted)
            return false;

        client.IsDeleted = false;
        client.DeletedAt = null;
        client.Status = await ResolveClientStatusAsync(client.Id);
        client.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<List<ClientDto>> GetDeletedAsync()
    {
        return await _context.Clients
            .IgnoreQueryFilters()
            .Include(c => c.Trainer)
                .ThenInclude(t => t!.User)
            .Include(c => c.ActivePackage)
            .Include(c => c.Location)
            .Where(c => c.IsDeleted)
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
                TrainingStartDate = c.TrainingStartDate,
                CreatedAt = c.CreatedAt,
                UpdatedAt = c.UpdatedAt,
                CreatedBy = c.CreatedBy,
                TrainerFullName = c.Trainer != null
                    ? c.Trainer.User.FirstName + " " + c.Trainer.User.LastName
                    : null
            })
            .ToListAsync();
    }

    public async Task<List<ClientDto>> GetFilteredAsync(ClientFilterDto filter)
    {
        var query = ApplyAccessControl(BuildClientQuery());

        if (filter.TrainerId.HasValue)
            query = query.Where(c => c.TrainerId == filter.TrainerId.Value);

        if (filter.LocationId.HasValue)
            query = query.Where(c => c.LocationId == filter.LocationId.Value);

        if (!string.IsNullOrWhiteSpace(filter.Status))
            query = query.Where(c => c.Status == filter.Status);

        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var search = filter.Search.ToLower();
            query = query.Where(c =>
                c.FirstName.ToLower().Contains(search) ||
                c.LastName.ToLower().Contains(search) ||
                c.Email.ToLower().Contains(search));
        }

        return await query
            .OrderByDescending(c => c.CreatedAt)
            .ToListAsync();
    }

    private async Task<List<ClientWorkspaceSessionDto>> BuildUpcomingSessionsAsync(int clientId)
    {
        var now = DateTime.UtcNow;

        var participants = await _context.SessionParticipants
            .Include(p => p.Session)
                .ThenInclude(s => s.Trainer)
                    .ThenInclude(t => t.User)
            .Include(p => p.Session)
                .ThenInclude(s => s.Location)
            .Where(p => p.ClientId == clientId && p.Session.StartAt >= now)
            .OrderBy(p => p.Session.StartAt)
            .Take(8)
            .ToListAsync();

        return participants.Select(p => new ClientWorkspaceSessionDto
        {
            SessionId = p.SessionId,
            Title = p.Session.Title,
            StartAt = ToStudioDisplayDateTime(p.Session.StartAt),
            EndAt = ToStudioDisplayDateTime(p.Session.EndAt),
            Status = p.Session.Status,
            LocationName = p.Session.Location.Name,
            TrainerFullName = p.Session.Trainer.User.FirstName + " " + p.Session.Trainer.User.LastName,
            AttendanceStatus = p.AttendanceStatus,
            CountsAgainstPackage = p.CountsAgainstPackage,
            IsCountedFromPackage = p.IsCountedFromPackage
        }).ToList();
    }

    private async Task<List<ClientWorkspaceCountedSessionDto>> BuildCountedSessionsAsync(int clientId)
    {
        var participants = await _context.SessionParticipants
            .Include(p => p.Session)
                .ThenInclude(s => s.Trainer)
                    .ThenInclude(t => t.User)
            .Include(p => p.Session)
                .ThenInclude(s => s.Location)
            .Where(p => p.ClientId == clientId && p.IsCountedFromPackage)
            .OrderByDescending(p => p.Session.StartAt)
            .Take(12)
            .ToListAsync();

        return participants.Select(p => new ClientWorkspaceCountedSessionDto
        {
            SessionId = p.SessionId,
            Date = ToStudioDisplayDateTime(p.Session.StartAt),
            TrainerFullName = p.Session.Trainer.User.FirstName + " " + p.Session.Trainer.User.LastName,
            LocationName = p.Session.Location.Name,
            Status = p.Session.Status,
            SessionsCharged = p.SessionsCharged,
            PlannedBillingType = p.PlannedBillingType?.ToString() ?? string.Empty,
            ActualBillingType = p.ActualBillingType?.ToString() ?? string.Empty,
            ExpectedUnitPrice = p.ExpectedUnitPrice ?? 0,
            ActualUnitPrice = p.ActualUnitPrice ?? 0,
            BalanceDifference = p.BalanceDifference ?? 0
        }).ToList();
    }

    private static async Task<T?> TryLoadAsync<T>(Func<Task<T>> factory)
        where T : class
    {
        try
        {
            return await factory();
        }
        catch (InvalidOperationException)
        {
            return null;
        }
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

    private IQueryable<ClientDto> BuildClientQuery()
    {
        return _context.Clients
            .Include(c => c.Trainer)
                .ThenInclude(t => t!.User)
            .Include(c => c.Location)
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
                TrainingStartDate = c.TrainingStartDate,
                CreatedAt = c.CreatedAt,
                UpdatedAt = c.UpdatedAt,
                CreatedBy = c.CreatedBy,
                TrainerFullName = c.Trainer != null
                    ? c.Trainer.User.FirstName + " " + c.Trainer.User.LastName
                    : null
            });
    }

    private IQueryable<ClientDto> ApplyAccessControl(IQueryable<ClientDto> query)
    {
        if (_currentUser.IsOwner)
            return query;

        if (_currentUser.IsTrainer && !_currentUser.IsOwner && _currentUser.UserId.HasValue)
        {
            return query.Where(c =>
                c.TrainerId != null &&
                _context.Trainers.Any(t => t.Id == c.TrainerId && t.UserId == _currentUser.UserId.Value));
        }

        return query.Where(c => false);
    }

    private async Task<string> ResolveClientStatusAsync(int clientId)
    {
        var hasActivePackage = await _context.ClientPackages
            .AnyAsync(cp => cp.ClientId == clientId && cp.IsActive);

        return hasActivePackage ? "Active" : "Inactive";
    }

    private async Task EnsureActivePackageMatchesLocationAsync(int clientId, int locationId)
    {
        var hasMismatchedActivePackage = await _context.ClientPackages
            .AnyAsync(cp =>
                cp.ClientId == clientId &&
                cp.IsActive &&
                cp.Package.LocationId.HasValue &&
                cp.Package.LocationId.Value != locationId);

        if (hasMismatchedActivePackage)
            throw new InvalidOperationException("Client has an active package from another location. Change the package before changing the client's location.");
    }

    private async Task<ClientDto> GetProjectedById(int id)
    {
        var query = ApplyAccessControl(BuildClientQuery());

        return await query.FirstAsync(c => c.Id == id);
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

    private static DateTime? NormalizeNullableDate(DateTime? value)
    {
        if (!value.HasValue)
            return null;

        return DateTime.SpecifyKind(value.Value.Date, DateTimeKind.Utc);
    }
}
