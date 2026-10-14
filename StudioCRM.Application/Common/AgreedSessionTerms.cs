using System.Text.Json;
using StudioCRM.Domain.Entities;
using StudioCRM.Domain.Enums;

namespace StudioCRM.Application.Common;

public sealed record AgreedSessionTerm(SessionBillingType BillingType, int Limit, int OpeningUsed);

public static class AgreedSessionTerms
{
    public static List<AgreedSessionTerm>? Read(ClientPackage package)
    {
        if (package.AgreedSessionTermsJson is null) return null;
        var terms = JsonSerializer.Deserialize<List<AgreedSessionTerm>>(package.AgreedSessionTermsJson);
        if (terms is null || terms.Count == 0 ||
            terms.Any(t => !Enum.IsDefined(t.BillingType) || t.BillingType == SessionBillingType.Group ||
                t.Limit <= 0 || t.OpeningUsed < 0 || t.OpeningUsed > t.Limit) ||
            terms.Select(t => t.BillingType).Distinct().Count() != terms.Count ||
            terms.Sum(t => t.Limit) != package.TotalSessions || package.ExpectedUnitPrice <= 0)
            throw new InvalidOperationException("Invalid individual package agreement.");
        return terms;
    }

    public static void ValidateUsage(List<AgreedSessionTerm> terms, SessionBillingType type, int used, int charge)
    {
        var term = terms.SingleOrDefault(t => t.BillingType == type);
        if (term is null || charge <= 0 || used + term.OpeningUsed + charge > term.Limit)
            throw new InvalidOperationException("Session exceeds the agreed package limit for this training type.");
    }
}
