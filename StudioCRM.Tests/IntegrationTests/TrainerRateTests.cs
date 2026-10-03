using Microsoft.EntityFrameworkCore;
using StudioCRM.Application.DTOs.TrainerRates;
using StudioCRM.Domain.Entities;
using StudioCRM.Infrastructure.Services;

namespace StudioCRM.Tests.IntegrationTests;

public class TrainerRateTests
{
    [PostgresFact]
    public async Task PartialUpdatesPreserveOtherRatesAndHistory()
    {
        await using var database = await TestDatabase.CreateAsync();
        await using var db = database.Context();
        var trainer = new Trainer { User = new User { Email = "rates@example.test" } };
        db.Add(trainer);
        await db.SaveChangesAsync();
        var service = new TrainerRateService(db);
        await service.UpdateRatesAsync(trainer.Id, new UpdateTrainerRatesDto { HourlyRate = 50 });
        await service.UpdateRatesAsync(trainer.Id, new UpdateTrainerRatesDto { GroupSessionRate = 120 });
        var rates = await service.GetByTrainerIdAsync(trainer.Id);
        Assert.Equal(2, rates.Count);
        Assert.Equal(50m, rates.Single(r => r.SessionType == "Hourly").Rate);
        var original = rates.Single(r => r.SessionType == "Group");
        await service.UpdateRatesAsync(trainer.Id, new UpdateTrainerRatesDto { GroupSessionRate = 120 });
        Assert.Equal(2, await db.TrainerRates.CountAsync());
        await service.UpdateRatesAsync(trainer.Id, new UpdateTrainerRatesDto { GroupSessionRate = 150 });
        var history = await db.TrainerRates.OrderBy(r => r.Id).ToListAsync();
        var old = history.Single(r => r.Id == original.Id);
        var current = history.Single(r => r.SessionType == "Group" && r.IsActive);
        Assert.False(old.IsActive);
        Assert.Equal(current.ValidFrom, old.ValidTo);
        Assert.Equal(120m, old.Rate);
        Assert.Equal(150m, current.Rate);
        await service.UpdateRatesAsync(trainer.Id, new UpdateTrainerRatesDto { HourlyRate = 60 });
        Assert.Equal(current.Id, (await service.GetByTrainerIdAsync(trainer.Id)).Single(r => r.SessionType == "Group").Id);
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.UpdateRatesAsync(trainer.Id,
            new UpdateTrainerRatesDto { GroupSessionRate = -1 }));
        await service.UpdateRatesAsync(trainer.Id, new UpdateTrainerRatesDto { GroupSessionRate = 0 });
        Assert.Equal(0m, (await service.GetByTrainerIdAsync(trainer.Id)).Single(r => r.SessionType == "Group").Rate);
    }
}
