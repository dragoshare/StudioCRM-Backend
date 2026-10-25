namespace StudioCRM.Application.Settings;

public class OutlookSettings
{
    public string CalendarEmailDomain { get; set; } = string.Empty;

    // Enable only after the receiving domain has been verified.
    public bool UseCalendarEmails { get; set; }

    // Emergency switch (Outlook__EmergencyStop=true): CRM ignores Outlook webhooks and writes nothing to Outlook,
    // so no invitations or updates are e-mailed from trainers' mailboxes. Run a reconcile after switching it off.
    public bool EmergencyStop { get; set; }

    public string ClientId { get; set; } = string.Empty;

    public string ClientSecret { get; set; } = string.Empty;

    public string TenantId { get; set; } = "common";

    public string RedirectUri { get; set; } = string.Empty;

    // Address Microsoft sends calendar notifications to; also part of the Outlook instance binding of the database.
    public string WebhookUrl { get; set; } = string.Empty;

    public string Scopes { get; set; } = "openid profile offline_access Calendars.ReadWrite Contacts.ReadWrite MailboxSettings.ReadWrite User.Read";
}
