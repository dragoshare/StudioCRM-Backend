using Microsoft.EntityFrameworkCore;
using StudioCRM.Application.DTOs.Dashboard;
using StudioCRM.Application.Interfaces;
using StudioCRM.Infrastructure.Persistence;

namespace StudioCRM.Infrastructure.Services;

public class DashboardService : IDashboardService
{
    private readonly StudioCRMDbContext _context;

    public DashboardService(StudioCRMDbContext context)
    {
        _context = context;
    }

    public async Task<OwnerDashboardDto> GetOwnerDashboardAsync(int? locationId = null)
    {
        var today = DateTime.UtcNow.Date;
        var tomorrow = today.AddDays(1);
        var dayAfterTomorrow = today.AddDays(2);

        var trainersQuery = _context.Trainers.AsQueryable();
        if (locationId.HasValue)
        {
            trainersQuery = trainersQuery.Where(t =>
                t.TrainerLocations.Any(tl => tl.LocationId == locationId.Value));
        }

        var clientsQuery = _context.Clients.AsQueryable();
        if (locationId.HasValue)
        {
            clientsQuery = clientsQuery.Where(c => c.LocationId == locationId.Value);
        }

        var sessionsQuery = _context.Sessions
            .Include(s => s.Trainer).ThenInclude(t => t.User)
            .Include(s => s.Participants)
            .ThenInclude(p => p.Client)
            .Include(s => s.Location)
            .AsQueryable();

        if (locationId.HasValue)
        {
            sessionsQuery = sessionsQuery.Where(s => s.LocationId == locationId.Value);
        }

        var trainersCount = await trainersQuery.CountAsync();
        var activeClientsCount = await clientsQuery.CountAsync(c => c.Status == "Active");
        var plannedSessionsCount = await sessionsQuery.CountAsync(s => s.Status == "Planned");
        var activePackagesCount = await _context.Packages.CountAsync(p => p.IsActive);

        var todaySessions = await sessionsQuery
            .Where(s => s.StartAt >= today && s.StartAt < tomorrow)
            .OrderBy(s => s.StartAt)
            .Select(s => new DashboardSessionDto
            {
                Id = s.Id,
                Title = s.Title,
                StartAt = s.StartAt,
                EndAt = s.EndAt,
                TrainerFullName = s.Trainer.User.FirstName + " " + s.Trainer.User.LastName,
                ClientFullName = string.Join(" + ", s.Participants.Select(p => p.Client.FirstName + " " + p.Client.LastName)),
                Location = s.Location.Name,
                Status = s.Status
            })
            .ToListAsync();

        var tomorrowSessions = await sessionsQuery
            .Where(s => s.StartAt >= tomorrow && s.StartAt < dayAfterTomorrow)
            .OrderBy(s => s.StartAt)
            .Select(s => new DashboardSessionDto
            {
                Id = s.Id,
                Title = s.Title,
                StartAt = s.StartAt,
                EndAt = s.EndAt,
                TrainerFullName = s.Trainer.User.FirstName + " " + s.Trainer.User.LastName,
                ClientFullName = string.Join(" + ", s.Participants.Select(p => p.Client.FirstName + " " + p.Client.LastName)),
                Location = s.Location.Name,
                Status = s.Status
            })
            .ToListAsync();

        var recentClients = await clientsQuery
            .OrderByDescending(c => c.CreatedAt)
            .Take(5)
            .Select(c => new DashboardClientDto
            {
                Id = c.Id,
                FullName = c.FirstName + " " + c.LastName,
                Email = c.Email,
                Status = c.Status,
                CreatedAt = c.CreatedAt
            })
            .ToListAsync();

        return new OwnerDashboardDto
        {
            TrainersCount = trainersCount,
            ActiveClientsCount = activeClientsCount,
            PlannedSessionsCount = plannedSessionsCount,
            ActivePackagesCount = activePackagesCount,
            TodaySessions = todaySessions,
            TomorrowSessions = tomorrowSessions,
            RecentClients = recentClients
        };
    }
}