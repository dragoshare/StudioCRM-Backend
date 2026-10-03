using StudioCRM.Application.ClientPackages.Services;
using StudioCRM.Application.DTOs.ClientPackages;
using StudioCRM.Domain.Entities;
using StudioCRM.Domain.Enums;
using StudioCRM.Infrastructure.Services;

namespace StudioCRM.Tests.UnitTests;

public class PackageManagementTests
{
    [Fact]
    public void DisabledRenewalDoesNotCancelCurrentPackage()
    {
        var client = new Client { SubscriptionAutoRenewEnabled = false, NextPackageId = 5 };
        Assert.Equal("Active", SubscriptionService.ResolveSubscriptionStatus(client,
            new ClientPackage { TotalSessions = 10, UsedSessions = 2, PaymentStatus = PaymentStatus.Paid }));
        Assert.Equal("Inactive", SubscriptionService.ResolveSubscriptionStatus(client, null));
    }

    [Theory]
    [InlineData(2, 100)]
    [InlineData(3, 100)]
    [InlineData(10, 49)]
    [InlineData(10, -1)]
    public void CorrectionCannotEraseUsageOrPaidMoney(int sessions, int price)
    {
        var p = new ClientPackage { IsActive = true, UsedSessions = 3, AmountPaid = 50 };
        Assert.Throws<InvalidOperationException>(() => ClientPackageService.ValidateCorrection(p,
            new PackageChangeRequest { TotalSessions = sessions, TotalPrice = price }));
    }

    [Fact]
    public void ValidCorrectionCanExtendPackageAndReduceUnpaidDebt()
    {
        ClientPackageService.ValidateCorrection(new ClientPackage { UsedSessions = 3, AmountPaid = 50 },
            new PackageChangeRequest { TotalSessions = 12, TotalPrice = 50 });
    }
}
