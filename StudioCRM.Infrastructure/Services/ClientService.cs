using Microsoft.EntityFrameworkCore;
using StudioCRM.Application.DTOs.Billing;
using StudioCRM.Application.DTOs.Clients;
using StudioCRM.Application.DTOs.Subscriptions;
using StudioCRM.Application.DTOs.TrainingPlans;
using StudioCRM.Application.Interfaces;
using StudioCRM.Domain.Entities;
using StudioCRM.Domain.Enums;
using StudioCRM.Infrastructure.Persistence;

namespace StudioCRM.Infrastructure.Services;

public partial class ClientService : IClientService
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
        ValidateClientIdentity(request.FirstName, request.LastName, request.Email);
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

        await ValidateTrainerLocationAsync(request.TrainerId, request.LocationId);
        var client = new Client
        {
            TrainerId = request.TrainerId,
            LocationId = request.LocationId,
            FirstName = request.FirstName.Trim(),
            LastName = request.LastName.Trim(),
            Email = NormalizeContactEmail(request.Email),
            Source = "StaffCreation",
            PhoneNumber = request.PhoneNumber,
            Goal = request.Goal,
            Notes = request.Notes,
            BillingStatus = request.BillingStatus ?? "Pending",
            Status = "Inactive",
            NextSessionAt = NormalizeNullableDateTime(request.NextSessionAt),
            TrainingStartDate = NormalizeNullableDate(request.TrainingStartDate),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            CreatedBy = _currentUser.UserId
        };

        await _context.Clients.AddAsync(client);
        await _context.ClientLocationMemberships.AddAsync(new ClientLocationMembership
        {
            Client = client,
            LocationId = request.LocationId,
            IsHomeLocation = true,
            GroupAccessEnabled = false,
            Source = "StaffCreation",
            JoinedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        await _context.SaveChangesAsync();

        return await GetProjectedById(client.Id);
    }

    public async Task<List<ClientDto>> GetAllAsync()
    {
        var query = ApplyAccessControl(BuildClientQuery());

        var clients = await query
            .OrderByDescending(c => c.CreatedAt)
            .ToListAsync();

        return await EnrichClientListSummariesAsync(clients);
    }

    public async Task<ClientDto?> GetByIdAsync(int id)
    {
        var query = ApplyAccessControl(BuildClientQuery(includeArchived: true));

        var client = await query
            .FirstOrDefaultAsync(c => c.Id == id);

        if (client is null)
            return null;

        var clients = await EnrichClientListSummariesAsync(new List<ClientDto> { client });
        return clients[0];
    }

    public async Task<ClientWorkspaceDto?> GetWorkspaceAsync(int id)
    {
        var profile = await GetByIdAsync(id);

        if (profile is null)
            return null;

        var client = await ReadableClients()
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
                CanDeactivate = _currentUser.IsOwner && !client.IsDeleted,
                CanChangeTrainer = _currentUser.IsOwner && !client.IsDeleted,
                CanChangePackage = !client.IsDeleted,
                CanAddPayment = !client.IsDeleted,
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

        var client = await _context.Clients.Include(c => c.User).FirstOrDefaultAsync(c => c.Id == id);
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

        await ValidateTrainerLocationAsync(request.TrainerId, request.LocationId);
        ValidateClientIdentity(request.FirstName, request.LastName, request.Email);
        var before = ClientAuditState(client);
        client.TrainerId = request.TrainerId;
        await SetHomeLocationMembershipAsync(client, request.LocationId);
        client.LocationId = request.LocationId;
        ValidateClientIdentity(request.FirstName, request.LastName, request.Email);
        client.FirstName = request.FirstName.Trim();
        client.LastName = request.LastName.Trim();
        client.Email = NormalizeContactEmail(request.Email);
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

        AuditClient(client, "ProfileUpdated", before);
        await _context.SaveChangesAsync();

        return await GetProjectedById(id);
    }

    public async Task<List<ClientLegalConsentDto>> GetLegalConsentsAsync(int clientId)
    {
        if (await GetByIdAsync(clientId) is null) throw new KeyNotFoundException("Client not found.");
        var userId = await ReadableClients()
            .Where(x => x.Id == clientId)
            .Select(x => x.UserId)
            .FirstOrDefaultAsync();

        if (!userId.HasValue)
            return new List<ClientLegalConsentDto>();

        return await _context.UserLegalConsents
            .Where(x => x.UserId == userId.Value)
            .OrderByDescending(x => x.AcceptedAt)
            .Select(x => new ClientLegalConsentDto
            {
                Id = x.Id,
                LegalEntityId = x.LegalEntityId,
                LegalEntityName = x.LegalEntity.Name,
                DocumentType = x.DocumentType,
                DocumentVersion = x.DocumentVersion,
                DocumentUrl = x.DocumentUrl,
                Source = x.Source,
                AcceptedAt = x.AcceptedAt,
                IsCurrent = x.DocumentVersion == x.LegalEntity.TermsVersion &&
                    x.DocumentUrl == x.LegalEntity.TermsUrl
            })
            .ToListAsync();
    }

    private async Task SetHomeLocationMembershipAsync(Client client, int locationId)
    {
        var memberships = await _context.ClientLocationMemberships
            .Where(x => x.ClientId == client.Id)
            .ToListAsync();

        foreach (var membership in memberships.Where(x => x.IsHomeLocation))
        {
            membership.IsHomeLocation = false;
            membership.UpdatedAt = DateTime.UtcNow;
        }

        var target = memberships.FirstOrDefault(x => x.LocationId == locationId);
        if (target is null)
        {
            await _context.ClientLocationMemberships.AddAsync(new ClientLocationMembership
            {
                ClientId = client.Id,
                LocationId = locationId,
                IsHomeLocation = true,
                GroupAccessEnabled = false,
                Source = "HomeLocationChange",
                JoinedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });
            return;
        }

        target.IsHomeLocation = true;
        target.UpdatedAt = DateTime.UtcNow;
    }

    public async Task<bool> DeleteAsync(int id)
    {
        EnsureOwner();
        await using var transaction = await _context.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
        var client = await _context.Clients.FirstOrDefaultAsync(c => c.Id == id);
        if (client is null)
            return false;

        var check = await CheckArchiveAsync(id);
        if (!check.CanArchive)
            throw new InvalidOperationException(string.Join("; ", check.Blockers));
        await CancelClientInvitationsAsync(id);
        await RevokeClientAccessTokensAsync(client);

        var before = ClientAuditState(client);
        client.IsDeleted = true;
        client.DeletedAt = DateTime.UtcNow;
        client.Status = "Inactive";
        client.UpdatedAt = DateTime.UtcNow;

        AuditClient(client, "Archived", before);
        await _context.SaveChangesAsync();
        await transaction.CommitAsync();
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

        await ValidateTrainerLocationAsync(request.TrainerId, client.LocationId);
        var before = ClientAuditState(client);
        client.TrainerId = request.TrainerId;
        client.UpdatedAt = DateTime.UtcNow;
        AuditClient(client, "TrainerChanged", before);

        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> RestoreAsync(int id)
    {
        EnsureOwner();
        var client = await _context.Clients
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(c => c.Id == id);

        if (client is null || !client.IsDeleted)
            return false;

        var before = ClientAuditState(client);
        client.IsDeleted = false;
        client.DeletedAt = null;
        client.Status = await ResolveClientStatusAsync(client.Id);
        client.UpdatedAt = DateTime.UtcNow;
        AuditClient(client, "Restored", before);

        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<List<ClientDto>> GetDeletedAsync()
    {
        EnsureOwner();
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
                UserId = c.UserId,
                LoginEmail = c.User != null ? c.User.Email : null,
                IsArchived = c.IsDeleted,
                ArchivedAt = c.DeletedAt,
                PortalAccessStatus = c.IsDeleted || c.PortalAccessBlocked || (c.User != null && !c.User.IsActive) ? "Blocked"
                    : c.UserId != null ? "Active"
                    : _context.Invitations.Any(i => i.ClientId == c.Id && !i.IsAccepted && i.CancelledAt == null && i.ExpiresAt > DateTime.UtcNow) ? "Invited" : "NoAccount",
                TrainerId = c.TrainerId,
                ActivePackageId = c.ActivePackageId,
                LocationId = c.LocationId,
                LocationName = c.Location.Name,
                FirstName = c.FirstName,
                LastName = c.LastName,
                FullName = c.FirstName + " " + c.LastName,
                Email = c.Email,
                CalendarEmail = c.CalendarEmail,
                EmailContactUrl = c.Email == "" ? "" : "mailto:" + c.Email,
                PhoneNumber = c.PhoneNumber,
                PhoneContactUrl = c.PhoneNumber != null ? "tel:" + c.PhoneNumber : null,
                AvatarUrl = c.User != null ? c.User.AvatarUrl : null,
                Goal = c.Goal,
                Notes = c.Notes,
                BillingStatus = c.BillingStatus,
                Source = c.Source,
                PortalAccessMode = c.TrainerId.HasValue ? "FullCrm" : "GroupOnly",
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

        var clients = await query
            .OrderByDescending(c => c.CreatedAt)
            .ToListAsync();

        return await EnrichClientListSummariesAsync(clients);
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
            .Where(p => p.ClientId == clientId && p.Session.StartAt >= now && p.Session.Status != "Cancelled")
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

    private IQueryable<Client> ReadableClients() => _currentUser.IsOwner
        ? _context.Clients.IgnoreQueryFilters() : _context.Clients;

    private IQueryable<ClientDto> BuildClientQuery(bool includeArchived = false)
    {
        return (includeArchived ? ReadableClients() : _context.Clients)
            .Include(c => c.Trainer)
                .ThenInclude(t => t!.User)
            .Include(c => c.Location)
            .Select(c => new ClientDto
            {
                Id = c.Id,
                UserId = c.UserId,
                LoginEmail = c.User != null ? c.User.Email : null,
                IsArchived = c.IsDeleted,
                ArchivedAt = c.DeletedAt,
                PortalAccessStatus = c.IsDeleted || c.PortalAccessBlocked || (c.User != null && !c.User.IsActive) ? "Blocked"
                    : c.UserId != null ? "Active"
                    : _context.Invitations.Any(i => i.ClientId == c.Id && !i.IsAccepted && i.CancelledAt == null && i.ExpiresAt > DateTime.UtcNow) ? "Invited" : "NoAccount",
                TrainerId = c.TrainerId,
                ActivePackageId = c.ActivePackageId,
                LocationId = c.LocationId,
                LocationName = c.Location.Name,
                FirstName = c.FirstName,
                LastName = c.LastName,
                FullName = c.FirstName + " " + c.LastName,
                Email = c.Email,
                CalendarEmail = c.CalendarEmail,
                EmailContactUrl = c.Email == "" ? "" : "mailto:" + c.Email,
                PhoneNumber = c.PhoneNumber,
                PhoneContactUrl = c.PhoneNumber != null ? "tel:" + c.PhoneNumber : null,
                AvatarUrl = c.User != null ? c.User.AvatarUrl : null,
                Goal = c.Goal,
                Notes = c.Notes,
                BillingStatus = c.BillingStatus,
                Source = c.Source,
                PortalAccessMode = c.TrainerId.HasValue ? "FullCrm" : "GroupOnly",
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

    private async Task<List<ClientDto>> EnrichClientListSummariesAsync(List<ClientDto> clients)
    {
        if (clients.Count == 0)
            return clients;

        var clientIds = clients.Select(c => c.Id).ToList();

        var activePackages = await _context.ClientPackages
            .Where(cp => clientIds.Contains(cp.ClientId) && cp.IsActive)
            .OrderByDescending(cp => cp.PurchaseDate)
            .ThenByDescending(cp => cp.Id)
            .Select(cp => new
            {
                cp.ClientId,
                cp.Id,
                cp.Name,
                cp.TotalSessions,
                cp.UsedSessions,
                cp.PaymentStatus
            })
            .ToListAsync();

        var activePackageByClientId = activePackages
            .GroupBy(cp => cp.ClientId)
            .ToDictionary(g => g.Key, g => g.First());

        var balancesByClientId = await _context.ClientBalanceTransactions
            .Where(t =>
                clientIds.Contains(t.ClientId) &&
                t.Type != BalanceTransactionType.PaymentCredit &&
                t.Type != BalanceTransactionType.PaymentReversal)
            .GroupBy(t => t.ClientId)
            .Select(g => new
            {
                ClientId = g.Key,
                Balance = g.Sum(t => t.Amount)
            })
            .ToDictionaryAsync(x => x.ClientId, x => x.Balance);

        foreach (var client in clients)
        {
            if (activePackageByClientId.TryGetValue(client.Id, out var activePackage))
            {
                client.ActiveClientPackageId = activePackage.Id;
                client.ActiveClientPackageName = activePackage.Name;
                client.ActivePackageTotalSessions = activePackage.TotalSessions;
                client.ActivePackageUsedSessions = activePackage.UsedSessions;
                client.ActivePackageRemainingSessions = Math.Max(
                    0,
                    activePackage.TotalSessions - activePackage.UsedSessions);
                client.ActivePackagePaymentStatus = activePackage.PaymentStatus.ToString();
            }

            client.CurrentBalance = balancesByClientId.TryGetValue(client.Id, out var balance)
                ? balance
                : 0;
        }

        return clients;
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
