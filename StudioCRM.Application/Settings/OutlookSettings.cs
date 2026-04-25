namespace StudioCRM.Application.Settings;

public class OutlookSettings
{
    public string ClientId { get; set; } = string.Empty;

    public string ClientSecret { get; set; } = string.Empty;

    public string TenantId { get; set; } = "common";

    public string RedirectUri { get; set; } = string.Empty;

    public string Scopes { get; set; } = "openid profile offline_access Calendars.ReadWrite User.Read";
}