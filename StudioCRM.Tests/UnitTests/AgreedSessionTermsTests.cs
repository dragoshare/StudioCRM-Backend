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
    public void AgreementRejectsOtherTrainingTypesAndDoubleChargesBeyondLimit()
    {
        var terms = AgreedSessionTerms.Read(Mixed())!;
        Assert.Throws<InvalidOperationException>(() => AgreedSessionTerms.ValidateUsage(terms, SessionBillingType.ThreeToOne, 0, 1));
        Assert.Throws<InvalidOperationException>(() => AgreedSessionTerms.ValidateUsage(terms, SessionBillingType.OneToOne, 4, 2));
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
