namespace StudioCRM.Application.Settings;

public class TpaySettings
{
    public bool UseSandbox { get; set; } = true;
    public Dictionary<string, TpayAccountSettings> Accounts { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

public class TpayAccountSettings
{
    public string ClientId { get; set; } = string.Empty;
    public string ClientSecret { get; set; } = string.Empty;
}
