using StudioCRM.Application.DTOs.Calendar;

namespace StudioCRM.Application.Interfaces.Calendar;

public interface IOutlookCalendarAuthService
{
    Task<CalendarConnectUrlDto> GetConnectUrlAsync();

    Task ConnectCallbackAsync(string code, string? state);

    Task<CalendarIntegrationStatusDto> GetStatusAsync();

    Task DisconnectAsync();
}