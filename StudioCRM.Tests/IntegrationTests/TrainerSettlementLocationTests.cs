using System.IO.Compression;
using StudioCRM.Application.Interfaces;
using StudioCRM.Domain.Entities;
using StudioCRM.Infrastructure.Services;

namespace StudioCRM.Tests.IntegrationTests;

public class TrainerSettlementLocationTests
{
    [PostgresFact]
    public async Task LocationFiltersTotalsAndDocumentAndSelectsItsContract()
    {
        await using var database = await TestDatabase.CreateAsync();
        await using var db = database.Context();
        var start = new DateTime(2026, 10, 5, 10, 0, 0, DateTimeKind.Utc);
        var trainer = new Trainer { User = new User { Email = "settlement@example.test" } };
        var first = new Location { Name = "Niepolomice" };
        var second = new Location { Name = "Other" };
        var uncovered = new Location { Name = "Without contract" };
        foreach (var location in new[] { first, second })
            db.Add(new TrainerContract
            {
                Trainer = trainer, ContractNumber = location.Name,
                ContractType = StudioCRM.Domain.Enums.TrainerContractType.Zlecenie,
                SignedAt = start.AddMonths(-1), ValidFrom = start.AddMonths(-1),
                ContractLocations = [new TrainerContractLocation { Location = location }]
            });
        db.Add(new TrainerRate { Trainer = trainer, SessionType = "Hourly", Rate = 50, ValidFrom = start.AddMonths(-1) });
        foreach (var (location, hours) in new[] { (first, 1), (second, 2), (uncovered, 3) })
            db.Add(new Session
            {
                Trainer = trainer, Location = location, StartAt = start, EndAt = start.AddHours(hours),
                Status = "Completed", ActualSessionType = "OneToOne"
            });
        await db.SaveChangesAsync();
        var service = new TrainerSettlementService(db, new Owner());
        var all = (await service.GetMonthlySettlementAsync(trainer.Id, 2026, 10))!;
        Assert.Equal(3m, all.TotalHours);
        Assert.Equal(3m, all.NonContractedTotalHours);
        var filtered = (await service.GetMonthlySettlementAsync(trainer.Id, 2026, 10, first.Id))!;
        Assert.Equal(first.Id, filtered.LocationId);
        Assert.Equal(1m, filtered.TotalHours);
        Assert.Equal(50m, filtered.TotalAmount);
        Assert.Equal(first.Id, Assert.Single(filtered.Items).LocationId);
        Assert.Empty(filtered.NonContractedItems);
        Assert.Equal(0m, filtered.NonContractedTotalHours);
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.GenerateWorkHoursDocumentAsync(trainer.Id, 2026, 10));
        var document = (await service.GenerateWorkHoursDocumentAsync(trainer.Id, 2026, 10, first.Id))!;
        Assert.Contains($"-lokalizacja-{first.Id}.docx", document.FileName);
        using var archive = new ZipArchive(new MemoryStream(document.Content));
        using var reader = new StreamReader(archive.GetEntry("word/document.xml")!.Open());
        var xml = await reader.ReadToEndAsync();
        Assert.Contains(first.Name, xml);
        Assert.DoesNotContain(second.Name, xml);
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.GenerateWorkHoursDocumentAsync(trainer.Id, 2026, 10, uncovered.Id));
    }

    private sealed class Owner : ICurrentUserService
    {
        public int? UserId => null;
        public string? Email => null;
        public List<string> Roles => ["Owner"];
        public bool IsAuthenticated => true;
        public bool IsOwner => true;
        public bool IsTrainer => false;
        public bool IsClient => false;
    }
}
