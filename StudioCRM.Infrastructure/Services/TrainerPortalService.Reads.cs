using Microsoft.EntityFrameworkCore;
using StudioCRM.Application.DTOs.Billing;
using StudioCRM.Application.DTOs.TrainerPortal;
namespace StudioCRM.Infrastructure.Services;

public partial class TrainerPortalService
{
    public async Task<TrainerDashboardSummaryDto?> GetDashboardSummaryAsync()
    {
        var now = DateTime.UtcNow;
        var localDate = TimeZoneInfo.ConvertTimeFromUtc(now, GetStudioTimeZone()).Date;
        var today = TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(localDate, DateTimeKind.Unspecified), GetStudioTimeZone());
        var tomorrow = TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(localDate.AddDays(1), DateTimeKind.Unspecified), GetStudioTimeZone());
        var sessions = _context.Sessions.AsNoTracking().Where(s => s.Trainer.UserId == _currentUser.UserId && s.Status != "Cancelled");
        var result = await _context.Trainers.Where(t => t.UserId == _currentUser.UserId)
            .Select(t => new TrainerDashboardSummaryDto
            {
                ActiveClientsCount = _context.Clients.Count(c => c.TrainerId == t.Id && c.Status == "Active"),
                TodaySessionsCount = _context.Sessions.Count(s => s.TrainerId == t.Id && s.Status != "Cancelled" && s.StartAt >= today && s.StartAt < tomorrow),
                UpcomingSessionsCount = _context.Sessions.Count(s => s.TrainerId == t.Id && s.Status != "Cancelled" && s.StartAt >= now)
            }).FirstOrDefaultAsync();
        if (result == null) return null;
        result.UpcomingSessions = await sessions.Where(s => s.StartAt >= now).OrderBy(s => s.StartAt).ThenBy(s => s.Id).Take(8)
            .Select(s => new TrainerDashboardSessionDto { Id = s.Id, Title = s.Title, StartAt = s.StartAt, EndAt = s.EndAt,
                Status = s.Status, LocationId = s.LocationId, LocationName = s.Location.Name, Capacity = s.PublicCapacity,
                BookedSeats = s.Participants.Count(p => p.AttendanceStatus != "CancelledInTime" && p.AttendanceStatus != "CancelledLate") }).ToListAsync();
        return result;
    }

    public async Task<PagedResultDto<TrainerPortalClientDto>> GetClientsPageAsync(PortalPageFilter filter)
    {
        filter.Validate();
        var query = _context.Clients.AsNoTracking().Where(c => c.Trainer != null && c.Trainer.UserId == _currentUser.UserId);
        if (filter.LocationId.HasValue) query = query.Where(c => c.LocationId == filter.LocationId);
        if (!string.IsNullOrWhiteSpace(filter.Status)) query = query.Where(c => c.Status == filter.Status);
        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var term = filter.Search.Trim().ToLower();
            query = query.Where(c => (c.FirstName + " " + c.LastName).ToLower().Contains(term) || c.Email.ToLower().Contains(term));
        }
        var count = await query.CountAsync();
        var ordered = filter.Sort switch
        {
            "nameDesc" => query.OrderByDescending(c => c.LastName).ThenByDescending(c => c.FirstName).ThenBy(c => c.Id),
            "created" => query.OrderBy(c => c.CreatedAt).ThenBy(c => c.Id),
            "createdDesc" => query.OrderByDescending(c => c.CreatedAt).ThenByDescending(c => c.Id),
            _ => query.OrderBy(c => c.LastName).ThenBy(c => c.FirstName).ThenBy(c => c.Id)
        };
        var items = await ordered.Skip((filter.Page - 1) * filter.PageSize).Take(filter.PageSize)
            .Select(c => new TrainerPortalClientDto
            {
                ClientId = c.Id,
                TrainerId = c.TrainerId,
                TrainerFullName = c.Trainer != null ? c.Trainer.User.FirstName + " " + c.Trainer.User.LastName : null,
                LocationId = c.LocationId,
                ActivePackageId = c.ActivePackageId,
                UserId = c.UserId,
                PortalAccessStatus = c.PortalAccessBlocked || (c.User != null && !c.User.IsActive) ? "Blocked" : c.UserId != null ? "Active" : _context.Invitations.Any(i => i.ClientId == c.Id && !i.IsAccepted && i.CancelledAt == null && i.ExpiresAt > DateTime.UtcNow) ? "Invited" : "NoAccount",
                FullName = c.FirstName + " " + c.LastName,
                Email = c.Email,
                EmailContactUrl = c.Email == "" ? "" : "mailto:" + c.Email,
                PhoneNumber = c.PhoneNumber,
                PhoneContactUrl = c.PhoneNumber != null ? "tel:" + c.PhoneNumber : null,
                AvatarUrl = c.User != null ? c.User.AvatarUrl : null,
                Goal = c.Goal,
                Status = c.Status,
                BillingStatus = c.BillingStatus,
                LocationName = c.Location.Name,
                TrainingStartDate = c.TrainingStartDate,
                CreatedAt = c.CreatedAt
            })
            .ToListAsync();
        await EnrichPortalClientsAsync(items);
        return ReadPage(items, count, filter);
    }

    public async Task<PagedResultDto<GroupClientOptionDto>> GetGroupClientsAsync(PortalPageFilter filter)
    {
        filter.Validate();
        if (!filter.LocationId.HasValue) throw new InvalidOperationException("locationId is required.");
        if (!_currentUser.IsOwner && !await _context.TrainerLocations.AnyAsync(t =>
            t.Trainer.UserId == _currentUser.UserId && t.LocationId == filter.LocationId))
            throw new KeyNotFoundException("Location not found.");
        var query = _context.Clients.AsNoTracking().Where(c => c.LocationMemberships.Any(m =>
            m.LocationId == filter.LocationId && m.GroupAccessEnabled));
        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var term = filter.Search.Trim().ToLower();
            query = query.Where(c => (c.FirstName + " " + c.LastName).ToLower().Contains(term));
        }
        if (!string.IsNullOrWhiteSpace(filter.Status)) query = query.Where(c => c.Status == filter.Status);
        var count = await query.CountAsync();
        var items = await query.OrderBy(c => c.LastName).ThenBy(c => c.FirstName).ThenBy(c => c.Id)
            .Skip((filter.Page - 1) * filter.PageSize).Take(filter.PageSize)
            .Select(c => new GroupClientOptionDto { ClientId = c.Id, FullName = c.FirstName + " " + c.LastName }).ToListAsync();
        return ReadPage(items, count, filter);
    }

    private static PagedResultDto<T> ReadPage<T>(List<T> items, int total, PortalPageFilter filter) => new()
    { Items = items, TotalCount = total, Page = filter.Page, PageSize = filter.PageSize,
      TotalPages = (int)Math.Ceiling(total / (double)filter.PageSize) };
}
