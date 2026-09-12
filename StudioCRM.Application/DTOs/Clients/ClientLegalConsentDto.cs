namespace StudioCRM.Application.DTOs.Clients;

public class ClientLegalConsentDto
{
    public int Id { get; set; }

    public int LegalEntityId { get; set; }

    public string LegalEntityName { get; set; } = string.Empty;

    public string DocumentType { get; set; } = string.Empty;

    public string DocumentVersion { get; set; } = string.Empty;

    public string DocumentUrl { get; set; } = string.Empty;

    public string Source { get; set; } = string.Empty;

    public DateTime AcceptedAt { get; set; }

    public bool IsCurrent { get; set; }
}
