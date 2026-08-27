namespace StudioCRM.Domain.Entities;

public class PaymentProviderAccount
{
    public int Id { get; set; }

    public int LegalEntityId { get; set; }

    public int? LocationId { get; set; }

    public string Provider { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public string? MerchantId { get; set; }

    public string? PosId { get; set; }

    public string? AccountKey { get; set; }

    public bool IsActive { get; set; } = true;

    public bool WebhookSecretConfigured { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public LegalEntity LegalEntity { get; set; } = null!;

    public Location? Location { get; set; }
}
