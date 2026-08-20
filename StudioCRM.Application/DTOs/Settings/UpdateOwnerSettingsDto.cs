namespace StudioCRM.Application.DTOs.Settings;

public class UpdateOwnerSettingsDto
{
    public int? DefaultPackageValidityDays { get; set; }

    public int? DefaultSessionDurationMinutes { get; set; }

    public int? DefaultPaymentDueDays { get; set; }
}
