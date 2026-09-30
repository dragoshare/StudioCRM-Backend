using StudioCRM.Application.Common;
using StudioCRM.Domain.Enums;

namespace StudioCRM.Tests.UnitTests;

public class PackageTypeMapperTests
{
    [Theory]
    [InlineData(SessionBillingType.OneToOne, "Individual")]
    [InlineData(SessionBillingType.TwoToOne, "SemiPersonal")]
    [InlineData(SessionBillingType.ThreeToOne, "SemiPersonal")]
    [InlineData(SessionBillingType.FourToOne, "SemiPersonal")]
    [InlineData(SessionBillingType.Group, "Group")]
    [InlineData((SessionBillingType)0, "Unknown")]
    public void ClassifiesBillingTypes(SessionBillingType billingType, string expected)
        => Assert.Equal(expected, PackageTypeMapper.FromBillingType(billingType));
}
