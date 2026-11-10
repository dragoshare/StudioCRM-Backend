namespace StudioCRM.Infrastructure.Services.Calendar;

// A session's own Outlook event is the one in its CalendarEventLink. An import row that points at the session while
// the link names another event (e.g. the old trainer's calendar after the session moved) is a leftover copy:
// changing or deleting it in Outlook must not move or cancel the session.
public static class OutlookEventCopy
{
    public static bool IsLeftover(
        IEnumerable<(int CalendarIntegrationId, string ExternalEventId)> sessionLinks,
        int calendarIntegrationId,
        string externalEventId)
    {
        var links = sessionLinks.ToList();
        return links.Count > 0 && !links.Any(link =>
            link.CalendarIntegrationId == calendarIntegrationId &&
            string.Equals(link.ExternalEventId, externalEventId, StringComparison.Ordinal));
    }
}
