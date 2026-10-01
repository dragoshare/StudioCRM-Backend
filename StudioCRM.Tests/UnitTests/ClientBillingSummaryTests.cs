using StudioCRM.Application.DTOs.Billing;
using StudioCRM.Domain.Entities;
using StudioCRM.Domain.Enums;
using StudioCRM.Infrastructure.Services;

namespace StudioCRM.Tests.UnitTests;

public class ClientBillingSummaryTests
{
    [Fact]
    public void ReversedPaymentHistoryStillBlocksDeletionEvenWithZeroPaidAndNoUsedEntries()
    {
        var package = new ClientPackageBillingDto { AmountPaid = 0, UsedSessions = 0 };
        Assert.Equal("PaymentHistory", ClientPackageDeletion.ResolveBlockReason(package, true, false, false));
        Assert.Equal("BalanceHistory", ClientPackageDeletion.ResolveBlockReason(package, false, true, false));
        Assert.Equal("CountedSessions", ClientPackageDeletion.ResolveBlockReason(package, false, false, true));
        Assert.Null(ClientPackageDeletion.ResolveBlockReason(package, false, false, false));
        package.ClosureDisposition = "Retained";
        Assert.Equal("ClosedPackage", ClientPackageDeletion.ResolveBlockReason(package, false, false, false));
    }

    [Fact]
    public void OutstandingAmountsIncludeAllPackageDebtsAndKeepCurrenciesSeparate()
    {
        var result = ClientPaymentService.BuildOutstandingAmounts(new[]
        {
            new ClientPackageBillingDto { Currency = "PLN", AmountDue = 100, IsActive = true },
            new ClientPackageBillingDto { Currency = "PLN", AmountDue = 1, IsActive = true },
            new ClientPackageBillingDto { Currency = "PLN", AmountDue = 20, IsActive = false },
            new ClientPackageBillingDto { Currency = "PLN", AmountDue = 0, IsActive = true },
            new ClientPackageBillingDto { Currency = "EUR", AmountDue = 10, IsActive = true }
        });
        Assert.Equal(2, result.Count);
        Assert.Equal(121m, result.Single(x => x.Currency == "PLN").AmountDue);
        Assert.Equal(3, result.Single(x => x.Currency == "PLN").PackageCount);
        Assert.Equal(10m, result.Single(x => x.Currency == "EUR").AmountDue);
    }

    [Theory]
    [InlineData(PaymentStatus.Paid, "Active")]
    [InlineData(PaymentStatus.Unpaid, "PendingPayment")]
    [InlineData(PaymentStatus.PartiallyPaid, "PendingPayment")]
    [InlineData(PaymentStatus.Overdue, "PendingPayment")]
    public void DisablingRenewalDoesNotCancelExistingCycle(PaymentStatus paymentStatus, string status)
    {
        var client = new Client { SubscriptionAutoRenewEnabled = false };
        var cycle = new ClientPackage { TotalSessions = 4, UsedSessions = 0, PaymentStatus = paymentStatus };
        Assert.Equal(status, SubscriptionService.ResolveSubscriptionStatus(client, cycle));
    }
}
