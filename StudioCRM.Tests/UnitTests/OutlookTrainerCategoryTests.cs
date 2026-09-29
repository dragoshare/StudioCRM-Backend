using System.Text.Json;
using StudioCRM.Domain.Entities;
using StudioCRM.Infrastructure.Services.Calendar;

namespace StudioCRM.Tests.UnitTests;

public class OutlookTrainerCategoryTests
{
    [Theory]
    [InlineData("Planned", 2)]
    [InlineData("Completed", 1)]
    [InlineData("Cancelled", 1)]
    public void ChangesOnlyPlannedSessionInstructor(string status, int expectedTrainer)
    {
        var session = new Session { TrainerId = 1, Status = status };
        var warning = OutlookTrainerCategoryResolver.ApplyToSession(session, Trainer(2, "Anna"), null);
        Assert.Equal(expectedTrainer, session.TrainerId);
        Assert.Equal(status != "Planned", warning != null);
    }

    [Fact]
    public void RemovedCategoryKeepsCurrentInstructor()
    {
        var session = new Session { TrainerId = 2 };
        OutlookTrainerCategoryResolver.ApplyToSession(session, null, null);
        Assert.Equal(2, session.TrainerId);
    }

    private static Trainer Trainer(int id, string category, int location = 4) => new()
    {
        Id = id, OutlookCategoryName = category, Status = "Active",
        User = new User { IsActive = true },
        TrainerLocations = new List<TrainerLocation> { new() { LocationId = location } }
    };

    [Fact]
    public void ResolvesNameIgnoringCaseAndOtherCategories()
    {
        var trainer = Trainer(2, "Anna");
        var result = OutlookTrainerCategoryResolver.Resolve(
            JsonSerializer.Serialize(new[] { "Inne", " anna " }), 4, new[] { trainer });
        Assert.Same(trainer, result.Trainer);
        Assert.Null(result.Warning);
    }

    [Theory]
    [InlineData("Inne")]
    [InlineData("Czerwony")]
    public void UnassignedCategoryDoesNotSelectTrainer(string category)
    {
        var result = OutlookTrainerCategoryResolver.Resolve(
            JsonSerializer.Serialize(new[] { category }), 4, new[] { Trainer(1, "Anna") });
        Assert.Null(result.Trainer);
        Assert.Null(result.Warning);
    }

    [Theory]
    [InlineData("Jan")]
    [InlineData("Anna")]
    public void AmbiguousCategoriesDoNotSelectTrainer(string secondCategory)
    {
        var result = OutlookTrainerCategoryResolver.Resolve(
            JsonSerializer.Serialize(new[] { "Anna", secondCategory }), 4,
            new[] { Trainer(1, "Anna"), Trainer(2, secondCategory) });
        Assert.Null(result.Trainer);
        Assert.NotNull(result.Warning);
    }

    [Fact]
    public void RejectsTrainerOutsideLocation()
    {
        var result = OutlookTrainerCategoryResolver.Resolve(
            JsonSerializer.Serialize(new[] { "Anna" }), 5, new[] { Trainer(1, "Anna") });
        Assert.Null(result.Trainer);
        Assert.NotNull(result.Warning);
    }

    [Fact]
    public void RejectsInactiveUser()
    {
        var trainer = Trainer(1, "Anna");
        trainer.User.IsActive = false;
        var result = OutlookTrainerCategoryResolver.Resolve(
            JsonSerializer.Serialize(new[] { "Anna" }), 4, new[] { trainer });
        Assert.Null(result.Trainer);
        Assert.NotNull(result.Warning);
    }
}
