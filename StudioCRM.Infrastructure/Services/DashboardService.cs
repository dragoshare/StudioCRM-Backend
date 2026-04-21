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

    public async Task<OwnerDashboardDto> GetOwnerDashboardAsync()
    {
        var today = DateTime.UtcNow.Date;
        var tomorrow = today.AddDays(1);
        var dayAfterTomorrow = today.AddDays(2);

        var trainersCount = await _context.Trainers.CountAsync();
        var activeClientsCount = await _context.Clients.CountAsync(c => c.Status == "Active");
        var plannedSessionsCount = await _context.Sessions.CountAsync(s => s.Status == "Planned");
        var activePackagesCount = await _context.Packages.CountAsync(p => p.IsActive);

        var todaySessions = await _context.Sessions
            .Include(s => s.Trainer).ThenInclude(t => t.User)
            .Include(s => s.Client)
            .Where(s => s.StartAt >= today && s.StartAt < tomorrow)
            .OrderBy(s => s.StartAt)
            .Select(s => new DashboardSessionDto
            {
                Id = s.Id,
                Title = s.Title,
                StartAt = s.StartAt,
                EndAt = s.EndAt,
                TrainerFullName = s.Trainer.User.FirstName + " " + s.Trainer.User.LastName,
                ClientFullName = s.Client.FirstName + " " + s.Client.LastName,
                Location = s.Location,
                Status = s.Status
            })
            .ToListAsync();

        var tomorrowSessions = await _context.Sessions
            .Include(s => s.Trainer).ThenInclude(t => t.User)
            .Include(s => s.Client)
            .Where(s => s.StartAt >= tomorrow && s.StartAt < dayAfterTomorrow)
            .OrderBy(s => s.StartAt)
            .Select(s => new DashboardSessionDto
            {
                Id = s.Id,
                Title = s.Title,
                StartAt = s.StartAt,
                EndAt = s.EndAt,
                TrainerFullName = s.Trainer.User.FirstName + " " + s.Trainer.User.LastName,
                ClientFullName = s.Client.FirstName + " " + s.Client.LastName,
                Location = s.Location,
                Status = s.Status
            })
            .ToListAsync();

        var recentClients = await _context.Clients
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