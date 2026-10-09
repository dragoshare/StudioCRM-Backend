using StudioCRM.Domain.Entities;
using StudioCRM.Infrastructure.Services;

namespace StudioCRM.Tests.UnitTests;

public class TrainerGroupRateTests
{
    private static readonly DateTime Start = new(2026, 10, 5, 10, 0, 0, DateTimeKind.Utc);

    [Theory]
    [InlineData(30, 1)]
    [InlineData(60, 5)]
    [InlineData(120, 12)]
    public void GroupPaysOnceRegardlessOfDurationAndAttendance(int minutes, int participants)
    {
        var session = MakeSession("Group", minutes);
        session.ActualParticipantsCount = participants;
        var rates = Rates();
        var item = Assert.Single(TrainerSettlementService.BuildSettlementItems([session], rates, new()));
        var cost = TrainerCostAnalysisService.BuildSessionRow(session, rates, []);
        Assert.Equal(120m, item.Amount);
        Assert.Equal(120m, item.Rate);
        Assert.Equal("PerSession", item.RateType);
        Assert.Equal(item.Amount, cost.PotentialTrainerCostAmount);
        Assert.Equal(120m, cost.GroupSessionRate);
    }

    [Theory]
    [InlineData("OneToOne", 100)]
    [InlineData("TwoToOne", 80)]
    [InlineData("ThreeToOne", 110)]
    [InlineData("FourToOne", 133)]
    public void IndividualAndSemiPersonalKeepHourlyCalculation(string type, int expected)
    {
        var item = Assert.Single(TrainerSettlementService.BuildSettlementItems([MakeSession(type, 120)], Rates(), new()));
        Assert.Equal((decimal)expected, item.Amount);
        Assert.Equal("Hourly", item.RateType);
    }

    [Fact]
    public void PublicGroupWithOneParticipantStillUsesFlatRate()
    {
        var session = MakeSession("OneToOne", 90);
        session.IsPubliclyBookable = true;
        Assert.Equal(120m, TrainerGroupRate.Resolve(session, "OneToOne", Rates()));
    }

    [Fact]
    public void HistoricalRateAndZeroAreRespectedAtChangeBoundary()
    {
        var rates = Rates();
        rates[1].ValidTo = Start.AddHours(1);
        rates[1].IsActive = false;
        rates.Add(new TrainerRate { TrainerId = 1, SessionType = "Group", Rate = 0, ValidFrom = Start.AddHours(1) });
        var session = MakeSession("Group", 60);
        Assert.Equal(120m, TrainerGroupRate.Resolve(session, "Group", rates));
        session.StartAt = Start.AddHours(1);
        Assert.Equal(0m, TrainerGroupRate.Resolve(session, "Group", rates));
        session.StartAt = Start.AddDays(-2);
        Assert.Null(TrainerGroupRate.Resolve(session, "Group", rates));
    }

    [Fact]
    public void UnconfiguredGroupsKeepExistingCalculation()
    {
        var rates = Rates().Where(r => r.SessionType == "Hourly").ToList();
        var item = Assert.Single(TrainerSettlementService.BuildSettlementItems([MakeSession("Group", 120)], rates, new()));
        Assert.Equal(100m, item.Amount);
        Assert.Equal("Hourly", item.RateType);
    }

    private static List<TrainerRate> Rates() =>
    [
        new() { TrainerId = 1, SessionType = "Hourly", Rate = 50, ValidFrom = Start.AddDays(-1) },
        new() { TrainerId = 1, SessionType = "Group", Rate = 120, ValidFrom = Start.AddDays(-1) }
    ];

    private static Session MakeSession(string type, int minutes) => new()
    {
        TrainerId = 1, PlannedSessionType = type, StartAt = Start, EndAt = Start.AddMinutes(minutes),
        Location = new Location { Name = "Studio" }, Trainer = new Trainer { User = new User() }
    };
}
