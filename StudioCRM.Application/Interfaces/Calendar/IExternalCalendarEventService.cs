using StudioCRM.Application.DTOs.Calendar;

namespace StudioCRM.Application.Interfaces.Calendar;

public interface IExternalCalendarEventService
{
    Task<List<ExternalCalendarEventDto>> GetImportedEventsAsync();

    Task<int> ConvertToSessionAsync(int importedEventId, ConvertExternalEventToSessionDto request);
}