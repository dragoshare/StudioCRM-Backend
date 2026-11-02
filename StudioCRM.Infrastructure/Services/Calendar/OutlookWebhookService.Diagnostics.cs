using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using StudioCRM.Application.DTOs.Calendar;

namespace StudioCRM.Infrastructure.Services.Calendar;

public partial class OutlookWebhookService
{
    private static readonly TimeSpan MaxDiagnosticsRange = TimeSpan.FromDays(62);

    // Read-only: reads the trainer's Outlook and CRM's rows, imports and changes nothing (only the access token
    // may be refreshed, as on every Outlook call).
    public async Task<OutlookImportDiagnosticsDto> DiagnoseImportAsync(OutlookImportDiagnosticsRequestDto request)
    {
        if (!_currentUser.IsOwner)
            throw new UnauthorizedAccessException("Only the owner can diagnose Outlook imports.");
        var now = DateTime.UtcNow;
        var from = NormalizeUtc(request.From) ?? now.Date.AddDays(-7);
        var to = NormalizeUtc(request.To) ?? now.Date.AddDays(30);
        if (to <= from || to - from > MaxDiagnosticsRange)
            throw new InvalidOperationException("The range must end after it starts and span at most 62 days.");

        var trainer = await _context.Trainers.AsNoTracking()
            .Where(t => t.Id == request.TrainerId)
            .Select(t => new { t.Id, t.UserId, Name = t.User.FirstName + " " + t.User.LastName })
            .FirstOrDefaultAsync() ?? throw new KeyNotFoundException("Trainer not found.");
        var result = new OutlookImportDiagnosticsDto
        {
            TrainerId = trainer.Id, TrainerName = trainer.Name, RangeStartAt = from, RangeEndAt = to
        };

        var integration = await _context.CalendarIntegrations
            .Where(i => i.UserId == trainer.UserId && i.Provider == "Outlook" && i.IsActive)
            .OrderByDescending(i => i.Id)
            .FirstOrDefaultAsync();
        if (integration is null)
            return result;
        result.IntegrationActive = true;
        result.IntegrationEmail = integration.Email;
        var subscription = await _context.CalendarSubscriptions.AsNoTracking()
            .Where(s => s.CalendarIntegrationId == integration.Id)
            .OrderByDescending(s => s.ExpiresAt)
            .FirstOrDefaultAsync();
        result.SubscriptionExpiresAt = subscription?.ExpiresAt;
        result.SubscriptionActive = subscription is { IsActive: true } && subscription.ExpiresAt > now;

        await _tokenService.EnsureValidAccessTokenAsync(integration);
        var outlookEvents = await GetCalendarViewEventsAsync(integration, from, to);
        var eventIds = outlookEvents.Keys.ToList();
        var rows = await _context.ExternalCalendarEvents.AsNoTracking()
            .Where(x => x.CalendarIntegrationId == integration.Id && eventIds.Contains(x.ExternalEventId))
            .ToListAsync();
        var rowsById = rows.GroupBy(x => x.ExternalEventId).ToDictionary(g => g.Key, g => g.First());
        var sessionIds = rows.Where(x => x.SessionId.HasValue).Select(x => x.SessionId!.Value).Distinct().ToList();
        var sessions = await _context.Sessions.AsNoTracking()
            .Where(s => sessionIds.Contains(s.Id))
            .Select(s => new { s.Id, s.Status, Trainer = s.Trainer.User.FirstName + " " + s.Trainer.User.LastName })
            .ToDictionaryAsync(s => s.Id);
        var links = await _context.CalendarEventLinks.AsNoTracking()
            .Where(l => sessionIds.Contains(l.SessionId) && l.Provider == "Outlook")
            .Select(l => new { l.SessionId, l.CalendarIntegrationId, l.ExternalEventId })
            .ToListAsync();
        var cutoff = _configuration["Outlook:ImportSessionsFrom"];

        foreach (var (externalEventId, root) in outlookEvents)
        {
            var attendees = ReadAttendeeEmails(root);
            var locationName = root.TryGetProperty("location", out var location) &&
                location.TryGetProperty("displayName", out var displayName) ? displayName.GetString() : null;
            var locationEmail = await ResolveLocationEmailAsync(attendees, locationName, ReadLocationEmail(root));
            var startAt = ReadGraphDateTime(root, "start");
            var endAt = ReadGraphDateTime(root, "end");
            rowsById.TryGetValue(externalEventId, out var row);
            var sessionId = row?.SessionId;
            var session = sessionId.HasValue && sessions.TryGetValue(sessionId.Value, out var found) ? found : null;
            var sessionLinks = links.Where(l => l.SessionId == sessionId).ToList();
            var item = new OutlookEventDiagnosisDto
            {
                ExternalEventId = externalEventId,
                Subject = root.TryGetProperty("subject", out var subject) ? subject.GetString() ?? "" : "",
                StartAt = startAt,
                EndAt = endAt,
                Type = root.TryGetProperty("type", out var type) ? type.GetString() : null,
                SeriesMasterId = root.TryGetProperty("seriesMasterId", out var master) ? master.GetString() : null,
                Categories = ReadCategories(root),
                LocationName = locationName,
                LocationEmail = locationEmail,
                OrganizerEmail = root.TryGetProperty("organizer", out var organizer) &&
                    organizer.TryGetProperty("emailAddress", out var organizerAddress) &&
                    organizerAddress.TryGetProperty("address", out var address) ? address.GetString() : null,
                Attendees = attendees,
                CreatedByCrm = CrmOutlookEventMarker.IsCrmCreated(
                    root.TryGetProperty("transactionId", out var transactionId) && transactionId.ValueKind == JsonValueKind.String
                        ? transactionId.GetString() : null),
                KnownLocation = await IsKnownCrmLocationAsync(locationName, locationEmail),
                BeforeImportCutoff = OutlookImportCutoff.IsBefore(startAt, cutoff),
                ImportedEventId = row?.Id,
                LastImportedAt = row?.ImportedAt,
                SessionId = sessionId,
                SessionStatus = session?.Status,
                SessionTrainerName = session?.Trainer,
                // The session's own event is in another calendar (or is another event): this row is a leftover.
                LinkedElsewhere = sessionId.HasValue && OutlookEventCopy.IsLeftover(
                    sessionLinks.Select(l => (l.CalendarIntegrationId, l.ExternalEventId)), integration.Id, externalEventId),
                Warnings = ReadDiagnosticWarnings(row?.MappingWarningsJson)
            };
            (item.Code, item.Explanation) = OutlookImportDiagnosis.Decide(new OutlookImportDiagnosis.Facts(
                item.Type, item.KnownLocation, item.CreatedByCrm, item.BeforeImportCutoff, endAt <= now,
                row is not null, row?.IsConvertedToSession == true, sessionId, session is not null, session?.Status,
                item.LinkedElsewhere, item.Warnings));
            result.Events.Add(item);
        }

        result.Events = result.Events.OrderBy(e => e.StartAt).ThenBy(e => e.ExternalEventId).ToList();
        result.Summary = result.Events.GroupBy(e => e.Code).OrderBy(g => g.Key).ToDictionary(g => g.Key, g => g.Count());
        return result;
    }

    private static DateTime? NormalizeUtc(DateTime? value) => value is null ? null
        : value.Value.Kind == DateTimeKind.Unspecified ? DateTime.SpecifyKind(value.Value, DateTimeKind.Utc) : value.Value.ToUniversalTime();

    private static List<string> ReadDiagnosticWarnings(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return new();
        try { return JsonSerializer.Deserialize<List<string>>(json) ?? new(); }
        catch (JsonException) { return new() { json }; }
    }
}
