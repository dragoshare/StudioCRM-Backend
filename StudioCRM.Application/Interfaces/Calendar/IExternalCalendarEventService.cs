using StudioCRM.Application.DTOs.Calendar;

namespace StudioCRM.Application.Interfaces.Calendar;

public interface IExternalCalendarEventService
{
    Task<List<ExternalCalendarEventDto>> GetImportedEventsAsync();

    Task<List<OutlookMappingIssueDto>> GetIssuesAsync();

    Task<int> ConvertToSessionAsync(int importedEventId, ConvertExternalEventToSessionDto request);

    Task SendInviteFromIssueAsync(string email);

    Task LinkClientFromIssueAsync(int clientId, string email);
    Task IgnoreIssueAsync(int externalEventId, string message);
}