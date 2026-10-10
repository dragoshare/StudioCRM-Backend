using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using StudioCRM.Domain.Entities;
using StudioCRM.Infrastructure.Persistence;

namespace StudioCRM.Tests.UnitTests;

public class ActivityLogTests
{
    private static StudioCRMDbContext Context() => new(new DbContextOptionsBuilder<StudioCRMDbContext>()
        .UseNpgsql("Host=localhost;Database=unused;Username=unused").Options);

    [Fact]
    public void ProfileChangeExcludesCredentialsAndLoginNoise()
    {
        using var db = Context();
        var user = new User { Id = 7, FirstName = "Before", PasswordHash = "secret-before" };
        db.Attach(user);
        user.FirstName = "After";
        user.PasswordHash = "secret-after";
        user.LastLoginAt = DateTime.UtcNow;
        var log = Assert.Single(ActivityLogCapture.Capture(db.ChangeTracker)).Complete(Guid.NewGuid(), DateTime.UtcNow, 1, "Owner");
        Assert.Equal("[\"firstName\"]", log.ChangedFieldsJson);
        Assert.DoesNotContain("secret", log.BeforeJson! + log.AfterJson!);
        Assert.DoesNotContain("password", log.AfterJson!);
        Assert.DoesNotContain("lastLoginAt", log.AfterJson!);
        Assert.Equal("Before", JsonDocument.Parse(log.BeforeJson!).RootElement.GetProperty("firstName").GetString());
        Assert.Equal("After", JsonDocument.Parse(log.AfterJson!).RootElement.GetProperty("firstName").GetString());
    }

    [Fact]
    public void UnchangedValuesAndTechnicalTimestampsDoNotCreateHistory()
    {
        using var db = Context();
        var user = new User { Id = 7 };
        db.Attach(user);
        user.PasswordHash = "new-secret";
        user.LastLoginAt = DateTime.UtcNow;
        user.UpdatedAt = DateTime.UtcNow.AddMinutes(1);
        Assert.Empty(ActivityLogCapture.Capture(db.ChangeTracker));
    }

    [Theory]
    [InlineData(false, true, "Archived")]
    [InlineData(true, false, "Restored")]
    public void SoftDeletionIsDistinguishedFromOtherUpdates(bool before, bool after, string operation)
    {
        using var db = Context();
        var client = new Client { Id = 4, IsDeleted = before };
        db.Attach(client);
        client.IsDeleted = after;
        var log = Assert.Single(ActivityLogCapture.Capture(db.ChangeTracker)).Complete(Guid.NewGuid(), DateTime.UtcNow, null, "System");
        Assert.Equal(operation, log.Operation);
        Assert.Equal("System", log.Source);
        Assert.Equal("4", log.EntityId);
    }

    [Fact]
    public void DeletionRetainsCompositeKeyAndOriginalSnapshot()
    {
        using var db = Context();
        var membership = new TrainerLocation { TrainerId = 2, LocationId = 3 };
        db.Attach(membership);
        db.Remove(membership);
        var log = Assert.Single(ActivityLogCapture.Capture(db.ChangeTracker)).Complete(Guid.NewGuid(), DateTime.UtcNow, 9, "Owner");
        Assert.Equal("Deleted", log.Operation);
        Assert.Null(log.AfterJson);
        Assert.Contains("TrainerId=2", log.EntityId);
        Assert.Contains("LocationId=3", log.EntityId);
    }

    [Fact]
    public void EveryAllowlistedFieldExistsInModel()
    {
        using var db = Context();
        foreach (var (type, fields) in ActivityLogCapture.Fields)
        {
            var entity = Assert.Single(db.Model.GetEntityTypes().Where(e => e.ClrType.Name == type));
            foreach (var field in fields) Assert.NotNull(entity.FindProperty(field));
        }
        Assert.Equal(ActivityLogCapture.Fields.Keys.Order(), ActivityLogLabels.TypeLabels.Keys.Order());
    }

    [Fact]
    public void EveryEntityHasReadableLabelIncludingNewRelatedGraph()
    {
        using var db = Context();
        var location = new Location { Name = "Niepołomice" };
        var user = new User { FirstName = "Piotr", LastName = "Trener" };
        var trainer = new Trainer { User = user };
        var client = new Client { FirstName = "Jan", LastName = "Kowalski", Location = location };
        var session = new Session { Title = "Trening poranny", Trainer = trainer, Location = location };
        var package = new Package { Name = "Pakiet 8" };
        var contract = new TrainerContract { Trainer = trainer, ContractNumber = "UM/2026" };
        db.AddRange(session, client, package, contract,
            new SessionParticipant { Session = session, Client = client },
            new TrainerLocation { Trainer = trainer, Location = location },
            new ClientLocationMembership { Client = client, Location = location },
            new ClientPackage { Client = client, Package = package, Name = "Pakiet 8" },
            new ClientPayment { Client = client },
            new ClientBalanceTransaction { Client = client },
            new TrainerRate { Trainer = trainer },
            new TrainerContractLocation { TrainerContract = contract, Location = location },
            new TrainerMonthlySettlement { Trainer = trainer, Year = 2026, Month = 10 });

        var pending = ActivityLogCapture.Capture(db.ChangeTracker);
        // No database exists here: new relationships must be resolved through EF temporary keys.
        ActivityLogLabels.Resolve(db, pending);
        var labels = pending.Select(c => c.Complete(Guid.NewGuid(), DateTime.UtcNow, null, "System"))
            .ToDictionary(c => c.EntityType, c => c.EntityLabel);
        var expected = new Dictionary<string, string>
        {
            ["Session"] = "Trening poranny",
            ["SessionParticipant"] = "Uczestnictwo w treningu — Jan Kowalski",
            ["Client"] = "Jan Kowalski",
            ["Trainer"] = "Trener — Piotr Trener",
            ["User"] = "Piotr Trener",
            ["TrainerLocation"] = "Lokalizacja trenera — Piotr Trener — Niepołomice",
            ["ClientLocationMembership"] = "Dostęp klienta do lokalizacji — Jan Kowalski — Niepołomice",
            ["Package"] = "Pakiet 8",
            ["ClientPackage"] = "Pakiet klienta — Jan Kowalski — Pakiet 8",
            ["ClientPayment"] = "Płatność — Jan Kowalski",
            ["ClientBalanceTransaction"] = "Zmiana salda — Jan Kowalski",
            ["TrainerRate"] = "Stawka trenera — Piotr Trener",
            ["TrainerContract"] = "Umowa trenera — Piotr Trener — UM/2026",
            ["TrainerContractLocation"] = "Lokalizacja umowy trenera — Piotr Trener — UM/2026 — Niepołomice",
            ["TrainerMonthlySettlement"] = "Rozliczenie trenera — Piotr Trener — 2026-10"
        };
        Assert.Equal(expected.Keys.Order(), labels.Keys.Order());
        foreach (var (type, label) in expected) Assert.Equal(label, labels[type]);
    }
}
