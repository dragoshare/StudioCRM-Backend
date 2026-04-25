using Microsoft.EntityFrameworkCore;
using StudioCRM.Application.DTOs.Calendar;
using StudioCRM.Application.Interfaces;
using StudioCRM.Application.Interfaces.Calendar;
using StudioCRM.Domain.Entities;
using StudioCRM.Infrastructure.Persistence;

namespace StudioCRM.Infrastructure.Services.Calendar;

public class ExternalCalendarEventService : IExternalCalendarEventService
{
    private readonly StudioCRMDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public ExternalCalendarEventService(
        StudioCRMDbContext context,
        ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<List<ExternalCalendarEventDto>> GetImportedEventsAsync()
    {
        if (!_currentUser.UserId.HasValue)
            return new List<ExternalCalendarEventDto>();

        return await _context.ExternalCalendarEvents
            .Include(x => x.CalendarIntegration)
            .Where(x => x.CalendarIntegration.UserId == _currentUser.UserId.Value)
            .OrderByDescending(x => x.StartAt)
            .Select(x => new ExternalCalendarEventDto
            {
                Id = x.Id,
                Subject = x.Subject,
                BodyPreview = x.BodyPreview,
                StartAt = x.StartAt,
                EndAt = x.EndAt,
                LocationName = x.LocationName,
                OrganizerEmail = x.OrganizerEmail,
                IsConvertedToSession = x.IsConvertedToSession,
                SessionId = x.SessionId,
                ImportedAt = x.ImportedAt
            })
            .ToListAsync();
    }

    public async Task<int> ConvertToSessionAsync(int importedEventId, ConvertExternalEventToSessionDto request)
    {
        if (!_currentUser.UserId.HasValue)
            throw new InvalidOperationException("User is not authenticated.");

        var importedEvent = await _context.ExternalCalendarEvents
            .Include(x => x.CalendarIntegration)
            .FirstOrDefaultAsync(x =>
                x.Id == importedEventId &&
                x.CalendarIntegration.UserId == _currentUser.UserId.Value);

        if (importedEvent is null)
            throw new InvalidOperationException("Imported event does not exist.");

        if (importedEvent.IsConvertedToSession)
            throw new InvalidOperationException("Imported event already converted.");

        var trainer = await _context.Trainers
            .FirstOrDefaultAsync(t => t.UserId == _currentUser.UserId.Value);

        if (trainer is null)
            throw new InvalidOperationException("Current user is not trainer.");

        var clientExists = await _context.Clients.AnyAsync(c => c.Id == request.ClientId);
        if (!clientExists)
            throw new InvalidOperationException("Client does not exist.");

        var locationExists = await _context.Locations.AnyAsync(l => l.Id == request.LocationId);
        if (!locationExists)
            throw new InvalidOperationException("Location does not exist.");

        var session = new Session
        {
            Title = importedEvent.Subject,
            Note = importedEvent.BodyPreview,
            StartAt = importedEvent.StartAt,
            EndAt = importedEvent.EndAt,
            TrainerId = trainer.Id,
            LocationId = request.LocationId,
            StudioRoom = request.StudioRoom,
            Status = request.Status,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            CreatedBy = _currentUser.UserId
        };

        await _context.Sessions.AddAsync(session);
        await _context.SaveChangesAsync();

        await _context.SessionParticipants.AddAsync(new SessionParticipant
        {
            SessionId = session.Id,
            ClientId = request.ClientId,
            PackageId = request.PackageId,
            AttendanceStatus = "Planned",
            CountsAgainstPackage = true,
            SessionsCharged = 1,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });

        await _context.SaveChangesAsync();

        importedEvent.IsConvertedToSession = true;
        importedEvent.SessionId = session.Id;

        await _context.CalendarEventLinks.AddAsync(new CalendarEventLink
        {
            SessionId = session.Id,
            CalendarIntegrationId = importedEvent.CalendarIntegrationId,
            Provider = "Outlook",
            ExternalEventId = importedEvent.ExternalEventId,
            SyncedAt = DateTime.UtcNow
        });

        await _context.SaveChangesAsync();

        return session.Id;
    }
}