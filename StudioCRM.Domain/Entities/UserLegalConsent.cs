namespace StudioCRM.Domain.Entities;

public class UserLegalConsent
{
    public int Id { get; set; }

    public int UserId { get; set; }

    public int LegalEntityId { get; set; }

    public string DocumentType { get; set; } = "TermsOfService";

    public string DocumentVersion { get; set; } = string.Empty;

    public string DocumentUrl { get; set; } = string.Empty;

    public string Source { get; set; } = string.Empty;

    public DateTime AcceptedAt { get; set; } = DateTime.UtcNow;

    public User User { get; set; } = null!;

    public LegalEntity LegalEntity { get; set; } = null!;
}
