using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using StudioCRM.Application.DTOs.Public;
using StudioCRM.Application.Interfaces;
using StudioCRM.Application.Interfaces.Calendar;
using StudioCRM.Domain.Entities;
using StudioCRM.Domain.Enums;
using StudioCRM.Infrastructure.Persistence;

namespace StudioCRM.Infrastructure.Services;

public class PublicGroupClassService : IPublicGroupClassService
{
    private const int MaxPublicCapacity = 100;
    private const int DefaultPublicCapacity = 20;
    private const string StudioTimeZone = "Central European Standard Time";

    private readonly StudioCRMDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly IOutlookCalendarSyncService _outlookCalendarSyncService;
    private readonly ILogger<PublicGroupClassService> _logger;

    public PublicGroupClassService(
        StudioCRMDbContext context,
        ICurrentUserService currentUser,
        IOutlookCalendarSyncService outlookCalendarSyncService,
        ILogger<PublicGroupClassService> logger)
    {
        _context = context;
        _currentUser = currentUser;
        _outlookCalendarSyncService = outlookCalendarSyncService;
        _logger = logger;
    }

    public async Task<List<PublicGroupLocationDto>> GetLocationsAsync()
    {
        var now = DateTime.UtcNow;

        return await _context.Locations
            .Where(l =>
                l.IsActive &&
                (_context.Packages.Any(p =>
                    p.IsActive &&
                    p.IsPubliclyAvailable &&
                    p.BillingType == SessionBillingType.Group &&
                    (p.LocationId == l.Id || p.LocationId == null)) ||
                 _context.Sessions.Any(s =>
                    s.IsPubliclyBookable &&
                    s.Status == "Planned" &&
                    s.StartAt >= now &&
                    s.LocationId == l.Id)))
            .OrderBy(l => l.Name)
            .Select(l => new PublicGroupLocationDto
            {
                Id = l.Id,
                Name = l.Name,
                City = l.City,
                Address = l.Address
            })
            .ToListAsync();
    }

    public async Task<List<PublicGroupPackageDto>> GetPackagesAsync(int? locationId)
    {
        var query = _context.Packages
            .Include(p => p.Location)
            .Where(p =>
                p.IsActive &&
                p.IsPubliclyAvailable &&
                p.BillingType == SessionBillingType.Group);

        if (locationId.HasValue)
        {
            query = query.Where(p => p.LocationId == locationId.Value || p.LocationId == null);
        }

        return await query
            .OrderByDescending(p => p.LocationId.HasValue)
            .ThenBy(p => p.Price)
            .Select(p => MapPackage(p))
            .ToListAsync();
    }

    public async Task<PublicGroupPackageDto?> GetPackageBySlugAsync(string slug)
    {
        var normalizedSlug = NormalizeSlug(slug);

        if (normalizedSlug is null)
            return null;

        return await _context.Packages
            .Include(p => p.Location)
            .Where(p =>
                p.IsActive &&
                p.IsPubliclyAvailable &&
                p.BillingType == SessionBillingType.Group &&
                p.PublicSlug == normalizedSlug)
            .Select(p => MapPackage(p))
            .FirstOrDefaultAsync();
    }

    public async Task<List<PublicGroupClassDto>> GetClassesAsync(PublicGroupClassFilterDto filter)
    {
        var query = BuildPublicClassQuery(filter);
        var currentClientId = await GetCurrentClientIdAsync();
        var limit = Math.Clamp(filter.Limit, 1, 100);
        var sessions = await query
            .OrderBy(s => s.StartAt)
            .Take(limit)
            .ToListAsync();

        return sessions
            .Select(s => MapClass(s, currentClientId))
            .ToList();
    }

    public async Task<PublicGroupClassDto?> GetClassAsync(int id)
    {
        var currentClientId = await GetCurrentClientIdAsync();
        var session = await BasePublicClassQuery()
            .FirstOrDefaultAsync(s => s.Id == id);

        return session is null ? null : MapClass(session, currentClientId);
    }

    public async Task<PublicGroupClassDto?> GetClassBySlugAsync(string slug)
    {
        var normalizedSlug = NormalizeSlug(slug);

        if (normalizedSlug is null)
            return null;

        var currentClientId = await GetCurrentClientIdAsync();
        var session = await BasePublicClassQuery()
            .FirstOrDefaultAsync(s => s.PublicSlug == normalizedSlug);

        return session is null ? null : MapClass(session, currentClientId);
    }

    public async Task<PublicGroupPurchaseDto> PurchasePackageForCurrentClientAsync(int packageId)
    {
        var client = await GetCurrentClientAsync();
        var package = await _context.Packages
            .Include(p => p.Location)
            .FirstOrDefaultAsync(p =>
                p.Id == packageId &&
                p.IsActive &&
                p.IsPubliclyAvailable &&
                p.BillingType == SessionBillingType.Group);

        if (package is null)
            throw new InvalidOperationException("Public group package does not exist.");

        if (package.LocationId.HasValue && package.LocationId.Value != client.LocationId)
            throw new InvalidOperationException("Package does not belong to the client's location.");

        if (package.SessionsLimit <= 0)
            throw new InvalidOperationException("Public group package entries count must be greater than zero.");

        var existingPackages = await _context.ClientPackages
            .Include(cp => cp.Package)
            .Where(cp =>
                cp.ClientId == client.Id &&
                cp.PackageId == package.Id &&
                cp.IsActive &&
                (cp.ValidUntil == null || cp.ValidUntil >= DateTime.UtcNow))
            .OrderBy(cp => cp.PurchaseDate)
            .ToListAsync();

        foreach (var existingPackage in existingPackages)
        {
            var remaining = await CountRemainingEntriesAsync(existingPackage);
            if (remaining > 0 || existingPackage.PaymentStatus != PaymentStatus.Paid)
                return MapPurchase(existingPackage, remaining);
        }

        var now = DateTime.UtcNow;
        var clientPackage = new ClientPackage
        {
            ClientId = client.Id,
            PackageId = package.Id,
            Name = package.Name,
            TotalSessions = package.SessionsLimit,
            SessionsPerWeek = package.SessionsPerWeek,
            UsedSessions = 0,
            TotalPrice = package.Price,
            OriginalPrice = package.Price,
            BalanceApplied = 0,
            AmountPaid = 0,
            ExpectedUnitPrice = package.SessionsLimit > 0
                ? decimal.Round(package.Price / package.SessionsLimit, 2)
                : package.Price,
            Currency = package.Currency,
            LocationId = package.LocationId ?? client.LocationId,
            ExpectedBillingType = SessionBillingType.Group,
            PaymentStatus = package.Price <= 0 ? PaymentStatus.Paid : PaymentStatus.Unpaid,
            PurchaseDate = now,
            ValidUntil = now.Date.AddDays(package.DurationDays),
            PaymentDueDate = package.Price <= 0 ? null : now.Date.AddDays(3),
            PaidAt = package.Price <= 0 ? now : null,
            ActivationMode = ClientPackageActivationMode.Immediately,
            RenewalSource = "GroupPublic",
            RequestedByUserId = _currentUser.UserId,
            ActivatedAt = now,
            ActivatedByUserId = _currentUser.UserId,
            IsActive = true
        };

        await _context.ClientPackages.AddAsync(clientPackage);
        client.Status = "Active";
        client.UpdatedAt = now;

        await _context.SaveChangesAsync();

        return MapPurchase(clientPackage, clientPackage.TotalSessions);
    }

    public async Task<PublicGroupBookingDto> BookCurrentClientAsync(int sessionId)
    {
        var client = await GetCurrentClientAsync();
        var session = await BasePublicClassQuery()
            .FirstOrDefaultAsync(s => s.Id == sessionId);

        if (session is null)
            throw new InvalidOperationException("Public group class does not exist.");

        if (session.StartAt <= DateTime.UtcNow)
            throw new InvalidOperationException("Past group class cannot be booked.");

        var existingParticipant = session.Participants
            .FirstOrDefault(p => p.ClientId == client.Id);

        if (existingParticipant is not null && IsActiveBooking(existingParticipant))
            throw new InvalidOperationException("Client is already booked for this class.");

        var bookedSeats = CountActiveBookings(session);
        var capacity = ResolveCapacity(session);

        if (bookedSeats >= capacity)
            throw new InvalidOperationException("Group class is fully booked.");

        var clientPackage = await ResolveBookableGroupPackageAsync(client.Id, session.LocationId);

        if (clientPackage is null)
            throw new InvalidOperationException("Client needs a paid group package with remaining entries before booking.");

        var remainingBeforeBooking = await CountRemainingEntriesAsync(clientPackage);

        if (remainingBeforeBooking <= 0)
            throw new InvalidOperationException("Client group package has no remaining entries.");

        if (existingParticipant is not null)
        {
            existingParticipant.AttendanceStatus = "Planned";
            existingParticipant.CountsAgainstPackage = true;
            existingParticipant.SessionsCharged = 1;
            existingParticipant.PackageId = clientPackage.PackageId;
            existingParticipant.ClientPackageId = clientPackage.Id;
            existingParticipant.PlannedBillingType = SessionBillingType.Group;
            existingParticipant.ExpectedUnitPrice = clientPackage.ExpectedUnitPrice;
            existingParticipant.UpdatedAt = DateTime.UtcNow;
        }
        else
        {
            existingParticipant = new SessionParticipant
            {
                SessionId = session.Id,
                ClientId = client.Id,
                PackageId = clientPackage.PackageId,
                ClientPackageId = clientPackage.Id,
                AttendanceStatus = "Planned",
                CountsAgainstPackage = true,
                SessionsCharged = 1,
                PlannedBillingType = SessionBillingType.Group,
                ExpectedUnitPrice = clientPackage.ExpectedUnitPrice,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            await _context.SessionParticipants.AddAsync(existingParticipant);
        }

        session.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();
        await TrySyncSessionToOutlookAsync(session.Id);

        return new PublicGroupBookingDto
        {
            SessionId = session.Id,
            ClientId = client.Id,
            SessionParticipantId = existingParticipant.Id,
            ClientPackageId = clientPackage.Id,
            Status = existingParticipant.AttendanceStatus,
            RemainingEntries = Math.Max(0, remainingBeforeBooking - 1)
        };
    }

    public async Task<bool> CancelCurrentClientBookingAsync(int sessionId)
    {
        var client = await GetCurrentClientAsync();
        var participant = await _context.SessionParticipants
            .Include(p => p.Session)
            .FirstOrDefaultAsync(p =>
                p.SessionId == sessionId &&
                p.ClientId == client.Id);

        if (participant is null)
            return false;

        if (participant.IsCountedFromPackage)
            throw new InvalidOperationException("Counted group class booking cannot be cancelled.");

        if (participant.Session.Status != "Planned")
            throw new InvalidOperationException("Only planned group class booking can be cancelled.");

        _context.SessionParticipants.Remove(participant);
        participant.Session.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();
        await TrySyncSessionToOutlookAsync(sessionId);

        return true;
    }

    private IQueryable<Session> BuildPublicClassQuery(PublicGroupClassFilterDto filter)
    {
        var query = BasePublicClassQuery();
        var from = NormalizeNullableStudioDateTime(filter.From) ?? DateTime.UtcNow;

        query = query.Where(s => s.StartAt >= from);

        if (filter.To.HasValue)
        {
            var to = NormalizeStudioDateTime(filter.To.Value);
            query = query.Where(s => s.StartAt <= to);
        }

        if (filter.LocationId.HasValue)
            query = query.Where(s => s.LocationId == filter.LocationId.Value);

        return query;
    }

    private IQueryable<Session> BasePublicClassQuery()
    {
        return _context.Sessions
            .Include(s => s.Trainer)
                .ThenInclude(t => t.User)
            .Include(s => s.Location)
            .Include(s => s.Participants)
            .Where(s =>
                s.IsPubliclyBookable &&
                s.Status == "Planned");
    }

    private async Task<Client> GetCurrentClientAsync()
    {
        if (!_currentUser.UserId.HasValue)
            throw new InvalidOperationException("Client is not authenticated.");

        var client = await _context.Clients
            .FirstOrDefaultAsync(c => c.UserId == _currentUser.UserId.Value);

        if (client is null)
            throw new InvalidOperationException("Client profile not found for current user.");

        return client;
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

    private async Task<ClientPackage?> ResolveBookableGroupPackageAsync(int clientId, int sessionLocationId)
    {
        var packages = await _context.ClientPackages
            .Include(cp => cp.Package)
            .Where(cp =>
                cp.ClientId == clientId &&
                cp.IsActive &&
                cp.ExpectedBillingType == SessionBillingType.Group &&
                cp.PaymentStatus == PaymentStatus.Paid &&
                (cp.ValidUntil == null || cp.ValidUntil >= DateTime.UtcNow) &&
                (cp.LocationId == null || cp.LocationId == sessionLocationId))
            .OrderBy(cp => cp.ValidUntil ?? DateTime.MaxValue)
            .ThenBy(cp => cp.PurchaseDate)
            .ToListAsync();

        foreach (var package in packages)
        {
            if (await CountRemainingEntriesAsync(package) > 0)
                return package;
        }

        return null;
    }

    private async Task<int> CountRemainingEntriesAsync(ClientPackage clientPackage)
    {
        var activeReservations = await _context.SessionParticipants
            .Where(p =>
                p.ClientPackageId == clientPackage.Id &&
                !p.IsCountedFromPackage &&
                p.AttendanceStatus != "CancelledInTime" &&
                p.AttendanceStatus != "CancelledLate" &&
                p.Session.Status != "Cancelled" &&
                p.Session.StartAt >= DateTime.UtcNow)
            .SumAsync(p => p.SessionsCharged);

        return Math.Max(0, clientPackage.TotalSessions - clientPackage.UsedSessions - activeReservations);
    }

    private static PublicGroupPackageDto MapPackage(Package package)
    {
        return new PublicGroupPackageDto
        {
            Id = package.Id,
            Name = package.Name,
            Description = package.Description,
            Price = package.Price,
            Currency = package.Currency,
            EntriesCount = package.SessionsLimit,
            DurationDays = package.DurationDays,
            LocationId = package.LocationId,
            LocationName = package.Location?.Name,
            PublicSlug = package.PublicSlug
        };
    }

    private static PublicGroupClassDto MapClass(Session session, int? currentClientId)
    {
        var bookedSeats = CountActiveBookings(session);
        var capacity = ResolveCapacity(session);

        return new PublicGroupClassDto
        {
            Id = session.Id,
            Title = session.Title,
            Note = session.Note,
            StartAt = ToStudioDisplayDateTime(session.StartAt),
            EndAt = ToStudioDisplayDateTime(session.EndAt),
            TrainerId = session.TrainerId,
            TrainerFullName = $"{session.Trainer.User.FirstName} {session.Trainer.User.LastName}".Trim(),
            LocationId = session.LocationId,
            LocationName = session.Location.Name,
            Capacity = capacity,
            BookedSeats = bookedSeats,
            AvailableSeats = Math.Max(0, capacity - bookedSeats),
            IsFullyBooked = bookedSeats >= capacity,
            IsBookedByCurrentClient = currentClientId.HasValue &&
                session.Participants.Any(p => p.ClientId == currentClientId.Value && IsActiveBooking(p)),
            PublicSlug = session.PublicSlug
        };
    }

    private static PublicGroupPurchaseDto MapPurchase(ClientPackage clientPackage, int remainingEntries)
    {
        return new PublicGroupPurchaseDto
        {
            ClientPackageId = clientPackage.Id,
            PackageId = clientPackage.PackageId,
            PackageName = clientPackage.Name,
            AmountDue = Math.Max(0, clientPackage.TotalPrice - clientPackage.AmountPaid),
            Currency = clientPackage.Currency,
            PaymentStatus = clientPackage.PaymentStatus.ToString(),
            EntriesCount = clientPackage.TotalSessions,
            RemainingEntries = remainingEntries,
            ValidUntil = clientPackage.ValidUntil.HasValue
                ? ToStudioDisplayDateTime(clientPackage.ValidUntil.Value)
                : null,
            Message = clientPackage.PaymentStatus == PaymentStatus.Paid
                ? null
                : "Package was created without checkout provider. Confirm payment before booking."
        };
    }

    private static int CountActiveBookings(Session session)
    {
        return session.Participants.Count(IsActiveBooking);
    }

    private static bool IsActiveBooking(SessionParticipant participant)
    {
        return participant.AttendanceStatus != "CancelledInTime" &&
            participant.AttendanceStatus != "CancelledLate";
    }

    private static int ResolveCapacity(Session session)
    {
        return Math.Clamp(session.PublicCapacity ?? DefaultPublicCapacity, 1, MaxPublicCapacity);
    }

    private async Task TrySyncSessionToOutlookAsync(int sessionId)
    {
        try
        {
            await _outlookCalendarSyncService.SyncSessionAsync(sessionId);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not sync public group class {SessionId} to Outlook.", sessionId);
        }
    }

    private static string? NormalizeSlug(string? value)
    {
        var normalized = value?.Trim().ToLowerInvariant();
        return string.IsNullOrWhiteSpace(normalized) ? null : normalized;
    }

    private static DateTime? NormalizeNullableStudioDateTime(DateTime? value)
    {
        return value.HasValue ? NormalizeStudioDateTime(value.Value) : null;
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
        foreach (var id in new[] { "Europe/Warsaw", StudioTimeZone })
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
}
