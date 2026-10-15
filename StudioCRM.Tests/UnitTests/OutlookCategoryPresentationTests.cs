using StudioCRM.Domain.Entities;
using StudioCRM.Infrastructure.Services.Calendar;

namespace StudioCRM.Tests.UnitTests;

public class OutlookCategoryPresentationTests
{
    [Fact]
    public void TrainerCategoryFallsBackToTrainerColour()
    {
        var session = new Session
        {
            OutlookCategoriesJson = "[\"Maria Chojnacka\"]",
            Trainer = new Trainer { OutlookCategoryName = "Maria Chojnacka", OutlookCategoryColor = "preset9" }
        };
        var result = OutlookCategoryPresentation.Read(session);
        Assert.Equal("Maria Chojnacka", result.Primary);
        Assert.Equal("preset9", result.PrimaryColor);
    }

    [Fact]
    public void StoredColourWinsAndMissingTrainerIsTolerated()
    {
        var session = new Session
        {
            OutlookCategoriesJson = "[\"Szymon Gorzula\",\"Konsultacje\"]",
            OutlookCategoryColorsJson = "[{\"Name\":\"Szymon Gorzula\",\"Color\":\"preset7\"}]",
            PrimaryOutlookCategory = "Szymon Gorzula"
        };
        var result = OutlookCategoryPresentation.Read(session);
        Assert.Equal("preset7", result.PrimaryColor);
        Assert.Equal(2, result.Colors.Count);
        Assert.Null(result.Colors.Single(c => c.Name == "Konsultacje").Color);
    }
}
