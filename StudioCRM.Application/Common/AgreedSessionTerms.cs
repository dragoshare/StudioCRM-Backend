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

    // Agreed pool used by a session of this type: its own pool, or for a bigger semi session (3:1, 4:1) not in the
    // agreement the agreed semi pool with the largest group below it (e.g. 3:1 uses 2:1). 1:1 never falls back.
    public static AgreedSessionTerm? TermFor(List<AgreedSessionTerm> terms, SessionBillingType type) =>
        terms.SingleOrDefault(t => t.BillingType == type) ??
        (SemiSize(type) is { } size
            ? terms.Where(t => SemiSize(t.BillingType) < size).OrderByDescending(t => SemiSize(t.BillingType)).FirstOrDefault()
            : null);

    // used: entries already charged to the pool TermFor(terms, type) (sessions of every type that use that pool).
    public static void ValidateUsage(List<AgreedSessionTerm> terms, SessionBillingType type, int used, int charge)
    {
        var term = TermFor(terms, type);
        if (term is null || charge <= 0 || used + term.OpeningUsed + charge > term.Limit)
            throw new InvalidOperationException("Session exceeds the agreed package limit for this training type.");
    }

    private static int? SemiSize(SessionBillingType type) => type switch
    {
        SessionBillingType.TwoToOne => 2,
        SessionBillingType.ThreeToOne => 3,
        SessionBillingType.FourToOne => 4,
        _ => null
    };
}
