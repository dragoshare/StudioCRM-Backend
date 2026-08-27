namespace StudioCRM.Application.DTOs.Billing;

public class PaymentProviderAccountDto
{
    public int Id { get; set; }

    public int LegalEntityId { get; set; }

    public string LegalEntityName { get; set; } = string.Empty;

    public int? LocationId { get; set; }

    public string? LocationName { get; set; }

    public string Provider { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public string? MerchantId { get; set; }

    public string? PosId { get; set; }

    public string? AccountKey { get; set; }

    public bool IsActive { get; set; }

    public bool WebhookSecretConfigured { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }
}
