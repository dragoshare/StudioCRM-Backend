using StudioCRM.Domain.Entities;

namespace StudioCRM.Application.Interfaces.Calendar;

public interface IOutlookTokenService
{
    Task EnsureValidAccessTokenAsync(CalendarIntegration integration, bool forceRefresh = false);
}
