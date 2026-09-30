using StudioCRM.Domain.Enums;

namespace StudioCRM.Application.Common;

public static class PackageTypeMapper
{
    public static string FromBillingType(SessionBillingType billingType) => billingType switch
    {
        SessionBillingType.OneToOne => "Individual",
        SessionBillingType.TwoToOne or SessionBillingType.ThreeToOne or SessionBillingType.FourToOne => "SemiPersonal",
        SessionBillingType.Group => "Group",
        _ => "Unknown"
    };
}
