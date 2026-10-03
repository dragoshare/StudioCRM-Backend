namespace StudioCRM.Application.Settings;

public class OutlookSettings
{
    public string CalendarEmailDomain { get; set; } = string.Empty;

    // Enable only after the receiving domain has been verified.
    public bool UseCalendarEmails { get; set; }

    public string ClientId { get; set; } = string.Empty;

    public string ClientSecret { get; set; } = string.Empty;

    public string TenantId { get; set; } = "common";

    public string RedirectUri { get; set; } = string.Empty;

    public string Scopes { get; set; } = "openid profile offline_access Calendars.ReadWrite Contacts.ReadWrite MailboxSettings.ReadWrite User.Read";
}
