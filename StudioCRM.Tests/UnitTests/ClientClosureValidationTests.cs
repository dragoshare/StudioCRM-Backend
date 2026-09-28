using StudioCRM.Domain.Entities;
using StudioCRM.Application.DTOs.Clients;
using StudioCRM.Infrastructure.Services;
using StudioCRM.Tests.IntegrationTests;

namespace StudioCRM.Tests.UnitTests;

public class ClientClosureValidationTests
{
    [Theory]
    [InlineData("Retain", 0, true)]
    [InlineData("Retain", 1, false)]
    [InlineData("Refund", 40, true)]
    [InlineData("Refund", 100, true)]
    [InlineData("Refund", 101, false)]
    [InlineData("Refund", 0, false)]
    [InlineData("Refund", -1, false)]
    [InlineData("Refund", 1.001, false)]
    [InlineData("Discard", 0, false)]
    public void ClosureRequiresExplicitValidDispositionAndBoundedRefund(string disposition, decimal amount, bool valid)
    {
        var package = new ClientPackage { AmountPaid = 100 };
        var decision = new CloseClientPackageRequest { Disposition = disposition, RefundAmount = amount };
        if (valid) ClientService.ValidateClosureDecision(package, decision);
        else Assert.Throws<InvalidOperationException>(() => ClientService.ValidateClosureDecision(package, decision));
    }
    [Fact]
    public void ClosedPackageCannotBeClosedAgain()
        => Assert.Throws<InvalidOperationException>(() => ClientService.ValidateClosureDecision(
            new ClientPackage { ClosureDisposition = "Refunded", AmountPaid = 100 }, new() { Disposition = "Retain" }));

    [Fact]
    public async Task TrainerCannotCloseArchiveOrConfirmRefundEvenWithDirectServiceAccess()
    {
        var service = new ClientService(null!, new ClientLifecycleV2Tests.Staff(1, false), null!, null!);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.CloseCooperationAsync(1, new()));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.DeleteAsync(1));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.ConfirmRefundAsync(1, 1, new()));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.RemoveLocationAsync(1, 1));
    }
}
