using StudioCRM.Domain.Entities;
using StudioCRM.Domain.Enums;

namespace StudioCRM.Application.Common;

public static class SessionBalancePolicy
{
    // Positive credit when the delivered training is cheaper; negative means an extra charge.
    public static decimal PerSession(SessionBillingType planned, SessionBillingType actual)
    {
        if (planned == SessionBillingType.Group && actual == SessionBillingType.Group) return 0;
        return Level(planned) - Level(actual);
    }

    public static decimal PerSession(ClientPackage package, SessionBillingType actual)
    {
        var terms = AgreedSessionTerms.Read(package);
        // Both training types were already purchased as part of this individual mixed agreement.
        if (terms is { Count: > 1 } && terms.Any(t => t.BillingType == actual)) return 0;
        return PerSession(package.ExpectedBillingType, actual);
    }

    private static decimal Level(SessionBillingType type) => type switch
    {
        SessionBillingType.OneToOne => 50m,
        SessionBillingType.TwoToOne => 20m,
        SessionBillingType.ThreeToOne => 10m,
        SessionBillingType.FourToOne => 0m,
        _ => throw new InvalidOperationException("This training type does not support personal/semi balance adjustments.")
    };
}
