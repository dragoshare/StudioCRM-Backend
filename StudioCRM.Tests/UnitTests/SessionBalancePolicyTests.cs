using StudioCRM.Application.Common;
using StudioCRM.Domain.Entities;
using StudioCRM.Domain.Enums;

namespace StudioCRM.Tests.UnitTests;

public class SessionBalancePolicyTests
{
    [Theory]
    [InlineData(1, 1, 0)] [InlineData(1, 2, 30)] [InlineData(1, 3, 40)] [InlineData(1, 4, 50)]
    [InlineData(2, 1, -30)] [InlineData(2, 2, 0)] [InlineData(2, 3, 10)] [InlineData(2, 4, 20)]
    [InlineData(3, 1, -40)] [InlineData(3, 2, -10)] [InlineData(3, 3, 0)] [InlineData(3, 4, 10)]
    [InlineData(4, 1, -50)] [InlineData(4, 2, -20)] [InlineData(4, 3, -10)] [InlineData(4, 4, 0)]
    public void FixedDifferenceAppliesInBothDirections(int planned, int actual, int amount)
        => Assert.Equal(amount, SessionBalancePolicy.PerSession((SessionBillingType)planned, (SessionBillingType)actual));

    [Fact]
    public void DiscountDoesNotCreateDebtForSameTrainingType()
    {
        var package = new ClientPackage { TotalSessions = 8, ExpectedUnitPrice = 103.75m, ExpectedBillingType = SessionBillingType.TwoToOne,
            AgreedSessionTermsJson = "[{\"BillingType\":2,\"Limit\":8,\"OpeningUsed\":0}]" };
        Assert.Equal(0m, SessionBalancePolicy.PerSession(package, SessionBillingType.TwoToOne));
        Assert.Equal(-30m, SessionBalancePolicy.PerSession(package, SessionBillingType.OneToOne));
    }

    [Fact]
    public void PurchasedMixedTypesDoNotGenerateAnExtraCharge()
    {
        var package = new ClientPackage { TotalSessions = 12, ExpectedUnitPrice = 117.50m, ExpectedBillingType = SessionBillingType.OneToOne,
            AgreedSessionTermsJson = "[{\"BillingType\":1,\"Limit\":6,\"OpeningUsed\":1},{\"BillingType\":2,\"Limit\":6,\"OpeningUsed\":0}]" };
        Assert.Equal(0m, SessionBalancePolicy.PerSession(package, SessionBillingType.TwoToOne));
    }

    [Fact]
    public void GroupSessionsCannotBeExchangedForPersonalSessions()
        => Assert.Throws<InvalidOperationException>(() => SessionBalancePolicy.PerSession(SessionBillingType.Group, SessionBillingType.OneToOne));
}
