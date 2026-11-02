namespace StudioCRM.Application.DTOs.Calendar;

// Owner-only, read-only: why the events of one trainer's Outlook did (not) become CRM sessions.
public class OutlookImportDiagnosticsRequestDto
{
    public int TrainerId { get; set; }

    // Defaults: 7 days back, 30 days ahead. At most 62 days.
    public DateTime? From { get; set; }

    public DateTime? To { get; set; }
}

public class OutlookImportDiagnosticsDto
{
    public int TrainerId { get; set; }
    public string TrainerName { get; set; } = string.Empty;
    public DateTime RangeStartAt { get; set; }
    public DateTime RangeEndAt { get; set; }

    // False: the trainer has no active Outlook connection, so CRM receives nothing from this calendar.
    public bool IntegrationActive { get; set; }
    public string? IntegrationEmail { get; set; }
    public DateTime? SubscriptionExpiresAt { get; set; }
    // False: notifications are not delivered; only a reconcile imports changes.
    public bool SubscriptionActive { get; set; }

    public Dictionary<string, int> Summary { get; set; } = new();
    public List<OutlookEventDiagnosisDto> Events { get; set; } = new();
}

public class OutlookEventDiagnosisDto
{
    public string ExternalEventId { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public DateTime StartAt { get; set; }
    public DateTime EndAt { get; set; }
    // singleInstance, occurrence, exception (changed occurrence of a series) or seriesMaster.
    public string? Type { get; set; }
    public string? SeriesMasterId { get; set; }
    public List<string> Categories { get; set; } = new();
    public string? LocationName { get; set; }
    public string? LocationEmail { get; set; }
    public string? OrganizerEmail { get; set; }
    public List<string> Attendees { get; set; } = new();
    public bool CreatedByCrm { get; set; }
    public bool KnownLocation { get; set; }
    public bool BeforeImportCutoff { get; set; }

    // CRM's row for this Outlook event (null: CRM never stored it).
    public int? ImportedEventId { get; set; }
    public DateTime? LastImportedAt { get; set; }
    public int? SessionId { get; set; }
    public string? SessionStatus { get; set; }
    public string? SessionTrainerName { get; set; }
    // The session is linked to an event in another calendar: this one is a leftover copy.
    public bool LinkedElsewhere { get; set; }
    public List<string> Warnings { get; set; } = new();

    // Stable code for the front, see docs/outlook-import-diagnostics.md.
    public string Code { get; set; } = string.Empty;
    public string Explanation { get; set; } = string.Empty;
}
