namespace StudioCRM.Application.DTOs.Invitations;

public class ValidateInvitationDto
{
    public int? ClientId { get; set; }
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public string Email { get; set; } = string.Empty;

    public string Role { get; set; } = string.Empty;

    public int LocationId { get; set; }

    public string LocationName { get; set; } = string.Empty;

    public int? TrainerId { get; set; }

    public string? TrainerName { get; set; }

    public DateTime ExpiresAt { get; set; }

    public int? LegalEntityId { get; set; }

    public string? LegalEntityName { get; set; }

    public bool TermsAcceptanceRequired { get; set; }

    public string? TermsVersion { get; set; }

    public string? TermsUrl { get; set; }
}
