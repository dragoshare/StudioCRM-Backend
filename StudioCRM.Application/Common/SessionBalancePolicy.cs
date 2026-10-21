using StudioCRM.Domain.Entities;
using StudioCRM.Domain.Enums;

namespace StudioCRM.Application.Common;

public static class SessionBalancePolicy
{
    // Price level of a 1:1 session above 1:4. Locations with a different personal/semi gap override it in settings.
    public const decimal DefaultOneToOneLevel = 50m;

    // Positive credit when the delivered training is cheaper; negative means an extra charge.
    public static decimal PerSession(SessionBillingType planned, SessionBillingType actual,
        decimal oneToOneLevel = DefaultOneToOneLevel)
    {
        if (planned == SessionBillingType.Group && actual == SessionBillingType.Group) return 0;
        return Level(planned, oneToOneLevel) - Level(actual, oneToOneLevel);
    }

    public static decimal PerSession(ClientPackage package, SessionBillingType actual,
        decimal oneToOneLevel = DefaultOneToOneLevel)
    {
        var terms = AgreedSessionTerms.Read(package);
        // Both training types were already purchased as part of this individual mixed agreement.
        if (terms is { Count: > 1 } && terms.Any(t => t.BillingType == actual)) return 0;
        // A type outside the mixed agreement (3:1 on a 1:1 + 2:1 agreement) is priced against the pool it uses.
        if (terms is { Count: > 1 } && AgreedSessionTerms.TermFor(terms, actual) is { } pool)
            return PerSession(pool.BillingType, actual, oneToOneLevel);
        return PerSession(package.ExpectedBillingType, actual, oneToOneLevel);
    }

    private static decimal Level(SessionBillingType type, decimal oneToOneLevel) => type switch
    {
        SessionBillingType.OneToOne => oneToOneLevel,
        SessionBillingType.TwoToOne => 20m,
        SessionBillingType.ThreeToOne => 10m,
        SessionBillingType.FourToOne => 0m,
        _ => throw new InvalidOperationException("This training type does not support personal/semi balance adjustments.")
    };
}
