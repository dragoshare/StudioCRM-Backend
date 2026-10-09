using StudioCRM.Infrastructure.Services;

namespace StudioCRM.Tests.UnitTests;

public class TpayNotificationContextTests
{
    [Theory]
    [InlineData("true")]
    [InlineData("TRUE")]
    [InlineData("True")]
    public void AcceptsSuccessfulStatusRegardlessOfLetterCase(string status)
    {
        ClientPaymentService.EnsureSuccessfulTpayNotification(status);
    }

    [Theory]
    [InlineData("false")]
    [InlineData("FALSE")]
    [InlineData("chargeback")]
    [InlineData("CHARGEBACK")]
    [InlineData("pending")]
    [InlineData("1")]
    [InlineData("")]
    [InlineData("true ")]
    public void RejectsUnsuccessfulOrMalformedStatus(string status)
    {
        var error = Assert.Throws<InvalidOperationException>(() => ClientPaymentService.EnsureSuccessfulTpayNotification(status));
        Assert.Contains("NotificationStatus=", error.Message);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("1")]
    public void SandboxAcceptsBothDocumentedTransactionModes(string testMode)
    {
        ClientPaymentService.EnsureTpayNotificationContext(142, "123", "123", testMode, "sandbox:pending", true);
    }

    [Theory]
    [InlineData("456", "0", "sandbox:pending", true)]
    [InlineData("123", "0", "sandbox:pending", false)]
    [InlineData("123", "1", "sandbox:pending", false)]
    [InlineData("123", "0", "production:pending", true)]
    [InlineData("123", "0", null, true)]
    [InlineData("123", "2", "sandbox:pending", true)]
    [InlineData("123", "", "sandbox:pending", true)]
    [InlineData("123", "0 ", "sandbox:pending", true)]
    public void RejectsWrongMerchantEnvironmentOrMalformedMode(string merchant, string mode, string? status, bool sandbox)
    {
        Assert.Throws<InvalidOperationException>(() =>
            ClientPaymentService.EnsureTpayNotificationContext(142, merchant, "123", mode, status, sandbox));
    }
}
