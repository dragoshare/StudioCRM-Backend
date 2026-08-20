namespace StudioCRM.Application.DTOs.Settings;

public class OwnerSettingsDto
{
    public int DefaultPackageValidityDays { get; set; }

    public int DefaultSessionDurationMinutes { get; set; }

    public int DefaultPaymentDueDays { get; set; }
}
