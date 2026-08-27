namespace StudioCRM.Application.DTOs.Billing;

public class UpsertPaymentProviderAccountRequest
{
    public int LegalEntityId { get; set; }

    public int? LocationId { get; set; }

    public string Provider { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public string? MerchantId { get; set; }

    public string? PosId { get; set; }

    public string? AccountKey { get; set; }

    public bool IsActive { get; set; } = true;

    public bool WebhookSecretConfigured { get; set; }
}
