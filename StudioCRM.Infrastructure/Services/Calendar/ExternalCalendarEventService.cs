using Microsoft.EntityFrameworkCore;
using StudioCRM.Application.DTOs.Calendar;
using StudioCRM.Application.Interfaces;
using StudioCRM.Application.Interfaces.Calendar;
using StudioCRM.Domain.Entities;
using StudioCRM.Infrastructure.Persistence;
using System.Text.Json;
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

        var events = await _context.ExternalCalendarEvents
            .Include(x => x.CalendarIntegration)
            .Where(x => x.CalendarIntegration.UserId == _currentUser.UserId.Value)
            .OrderByDescending(x => x.StartAt)
            .ToListAsync();

        return events.Select(x => new ExternalCalendarEventDto
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
            ImportedAt = x.ImportedAt,
            Warnings = ReadWarnings(x.MappingWarningsJson)
        }).ToList();
    }

    public async Task<int> ConvertToSessionAsync(int importedEventId, ConvertExternalEventToSessionDto request)
    {
        if (!_currentUser.UserId.HasValue)
            throw new InvalidOperationException("User is not authenticated.");

        var evt = await _context.ExternalCalendarEvents
            .Include(x => x.CalendarIntegration)
            .Include(x => x.Session)
            .FirstOrDefaultAsync(x =>
                x.Id == importedEventId &&
                x.CalendarIntegration.UserId == _currentUser.UserId.Value);

        if (evt is null)
            throw new InvalidOperationException("Event nie istnieje.");

        var mapper = new OutlookEventMapperService(_context);

        var result = await mapper.MapToSessionAsync(evt);

        if (result.Session == null)
            throw new InvalidOperationException(string.Join(" | ", result.Warnings));

        return result.Session.Id;
    }
    private static List<string> ReadWarnings(string? warningsJson)
    {
        if (string.IsNullOrWhiteSpace(warningsJson))
            return new List<string>();

        try
        {
            return JsonSerializer.Deserialize<List<string>>(warningsJson) ?? new List<string>();
        }
        catch
        {
            return new List<string>();
        }
    }
}