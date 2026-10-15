using StudioCRM.Domain.Entities;
using StudioCRM.Infrastructure.Services;

namespace StudioCRM.Tests.UnitTests;

public class TrainerContractCoverageTests
{
    private static TrainerContract Contract(string from, string? to, params int[] locations) => new()
    {
        Id = 1,
        ValidFrom = DateTime.Parse(from, null, System.Globalization.DateTimeStyles.AdjustToUniversal),
        ValidTo = to == null ? null : DateTime.Parse(to, null, System.Globalization.DateTimeStyles.AdjustToUniversal),
        ContractLocations = locations.Select(l => new TrainerContractLocation { LocationId = l }).ToList()
    };

    private static Session At(string startUtc, int location = 3) => new()
    {
        StartAt = DateTime.Parse(startUtc, null, System.Globalization.DateTimeStyles.AdjustToUniversal),
        LocationId = location
    };

    [Theory]
    // Contract from 15.10: the 14.10 session stays outside, 15.10 morning (Polish time) is covered.
    [InlineData("2026-10-14T16:00:00Z", false)]
    [InlineData("2026-10-15T05:00:00Z", true)]
    // Last contract day is inclusive (31.12 evening), the next day is not.
    [InlineData("2026-12-31T18:00:00Z", true)]
    [InlineData("2027-01-01T09:00:00Z", false)]
    public void SessionDateMustFallWithinContractPeriod(string sessionStart, bool covered)
    {
        var contract = Contract("2026-10-15T00:00:00Z", "2026-12-31T00:00:00Z", 3);
        Assert.Equal(covered, TrainerSettlementService.ResolveCoveringContract([contract], At(sessionStart)) != null);
    }

    [Fact]
    public void ContractDatePickedAsLocalMidnightCoversTheSameDay()
    {
        // 15.10 00:00 Polish time is stored as 14.10 22:00 UTC.
        var contract = Contract("2026-10-14T22:00:00Z", null, 3);
        Assert.NotNull(TrainerSettlementService.ResolveCoveringContract([contract], At("2026-10-15T05:00:00Z")));
        Assert.Null(TrainerSettlementService.ResolveCoveringContract([contract], At("2026-10-14T16:00:00Z")));
    }

    [Fact]
    public void SessionInLocationOutsideContractIsNotCovered()
        => Assert.Null(TrainerSettlementService.ResolveCoveringContract(
            [Contract("2026-06-01T00:00:00Z", null, 3)], At("2026-10-15T05:00:00Z", location: 4)));
}
