using StudioCRM.Application.Common;
using StudioCRM.Domain.Entities;
using StudioCRM.Domain.Enums;

namespace StudioCRM.Tests.UnitTests;

public class AgreedSessionTermsTests
{
    private static ClientPackage Mixed() => new()
    {
        TotalSessions = 12, ExpectedUnitPrice = 117.50m,
        AgreedSessionTermsJson = "[{\"BillingType\":1,\"Limit\":6,\"OpeningUsed\":1},{\"BillingType\":2,\"Limit\":6,\"OpeningUsed\":0}]"
    };

    [Fact]
    public void OpeningPersonalSessionLeavesFivePersonalAndSixSemiSessions()
    {
        var terms = AgreedSessionTerms.Read(Mixed())!;
        AgreedSessionTerms.ValidateUsage(terms, SessionBillingType.OneToOne, 4, 1);
        Assert.Throws<InvalidOperationException>(() => AgreedSessionTerms.ValidateUsage(terms, SessionBillingType.OneToOne, 5, 1));
        AgreedSessionTerms.ValidateUsage(terms, SessionBillingType.TwoToOne, 5, 1);
        Assert.Throws<InvalidOperationException>(() => AgreedSessionTerms.ValidateUsage(terms, SessionBillingType.TwoToOne, 6, 1));
    }

    [Fact]
    public void AgreementRejectsDoubleChargesBeyondLimit()
    {
        var terms = AgreedSessionTerms.Read(Mixed())!;
        Assert.Throws<InvalidOperationException>(() => AgreedSessionTerms.ValidateUsage(terms, SessionBillingType.OneToOne, 4, 2));
    }

    // 3:1 and 4:1 on a 1:1 + 2:1 agreement use the 2:1 pool with the semi credit (10 / 20 zł); 1:1 never falls back.
    [Fact]
    public void BiggerSemiSessionUsesSemiPoolWithCredit()
    {
        var package = Mixed();
        var terms = AgreedSessionTerms.Read(package)!;
        Assert.Equal(SessionBillingType.TwoToOne, AgreedSessionTerms.TermFor(terms, SessionBillingType.ThreeToOne)!.BillingType);
        Assert.Equal(SessionBillingType.TwoToOne, AgreedSessionTerms.TermFor(terms, SessionBillingType.FourToOne)!.BillingType);
        AgreedSessionTerms.ValidateUsage(terms, SessionBillingType.ThreeToOne, 5, 1);
        Assert.Throws<InvalidOperationException>(() => AgreedSessionTerms.ValidateUsage(terms, SessionBillingType.ThreeToOne, 6, 1));
        Assert.Equal(10m, SessionBalancePolicy.PerSession(package, SessionBillingType.ThreeToOne));
        Assert.Equal(20m, SessionBalancePolicy.PerSession(package, SessionBillingType.FourToOne));
        Assert.Equal(0m, SessionBalancePolicy.PerSession(package, SessionBillingType.TwoToOne));

        var semiOnly = AgreedSessionTerms.Read(new ClientPackage
        {
            TotalSessions = 12, ExpectedUnitPrice = 100m,
            AgreedSessionTermsJson = "[{\"BillingType\":3,\"Limit\":6,\"OpeningUsed\":0},{\"BillingType\":4,\"Limit\":6,\"OpeningUsed\":0}]"
        })!;
        Assert.Null(AgreedSessionTerms.TermFor(semiOnly, SessionBillingType.OneToOne));
        Assert.Null(AgreedSessionTerms.TermFor(semiOnly, SessionBillingType.TwoToOne));
    }

    [Fact]
    public void ChangedPackageSizeRequiresUpdatingAgreement()
    {
        var package = Mixed(); package.TotalSessions = 8;
        Assert.Throws<InvalidOperationException>(() => AgreedSessionTerms.Read(package));
    }

    [Fact]
    public void StandardRenewalHasNoIndividualAgreementByDefault()
        => Assert.Null(AgreedSessionTerms.Read(new ClientPackage()));
}
