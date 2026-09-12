using Microsoft.EntityFrameworkCore;
using StudioCRM.Application.DTOs.Public;
using StudioCRM.Domain.Entities;
using StudioCRM.Infrastructure.Persistence;

namespace StudioCRM.Infrastructure.Services;

internal static class LegalConsentManager
{
    private const string TermsDocumentType = "TermsOfService";

    public static async Task<PublicLegalRequirementsDto> GetRequirementsAsync(
        StudioCRMDbContext context,
        int locationId,
        int? userId = null)
    {
        var location = await context.Locations
            .Include(x => x.LegalEntity)
            .FirstOrDefaultAsync(x => x.Id == locationId && x.IsActive)
            ?? throw new InvalidOperationException("Location does not exist.");

        var legalEntity = location.LegalEntity;
        var acceptanceRequired = legalEntity is not null &&
            !string.IsNullOrWhiteSpace(legalEntity.TermsVersion) &&
            !string.IsNullOrWhiteSpace(legalEntity.TermsUrl);
        var isAccepted = !acceptanceRequired;

        if (acceptanceRequired && userId.HasValue)
        {
            isAccepted = await context.UserLegalConsents.AnyAsync(x =>
                x.UserId == userId.Value &&
                x.LegalEntityId == legalEntity!.Id &&
                x.DocumentType == TermsDocumentType &&
                x.DocumentVersion == legalEntity.TermsVersion);
        }

        return new PublicLegalRequirementsDto
        {
            LocationId = location.Id,
            LegalEntityId = legalEntity?.Id,
            LegalEntityName = legalEntity?.Name,
            AcceptanceRequired = acceptanceRequired,
            IsAccepted = isAccepted,
            TermsVersion = acceptanceRequired ? legalEntity!.TermsVersion : null,
            TermsUrl = acceptanceRequired ? legalEntity!.TermsUrl : null
        };
    }

    public static async Task AcceptAsync(
        StudioCRMDbContext context,
        int userId,
        int locationId,
        bool accepted,
        string? requestedVersion,
        string source)
    {
        var requirements = await GetRequirementsAsync(context, locationId, userId);
        if (!requirements.AcceptanceRequired || requirements.IsAccepted)
            return;

        if (!accepted || !string.Equals(
                requestedVersion?.Trim(),
                requirements.TermsVersion,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Current terms must be accepted before continuing.");
        }

        await context.UserLegalConsents.AddAsync(new UserLegalConsent
        {
            UserId = userId,
            LegalEntityId = requirements.LegalEntityId!.Value,
            DocumentType = TermsDocumentType,
            DocumentVersion = requirements.TermsVersion!,
            DocumentUrl = requirements.TermsUrl!,
            Source = source,
            AcceptedAt = DateTime.UtcNow
        });
    }

    public static async Task EnsureAcceptedAsync(
        StudioCRMDbContext context,
        int userId,
        int locationId)
    {
        var requirements = await GetRequirementsAsync(context, locationId, userId);
        if (requirements.AcceptanceRequired && !requirements.IsAccepted)
        {
            throw new InvalidOperationException(
                "Current terms for this location must be accepted before continuing.");
        }
    }
}
