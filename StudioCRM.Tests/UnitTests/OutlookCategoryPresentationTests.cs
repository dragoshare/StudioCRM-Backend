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

    // Outlook import stored the trainer category without a colour: the schedule must still use the trainer colour.
    [Fact]
    public void TrainerCategoryStoredWithoutColourFallsBackToTrainerColour()
    {
        var session = new Session
        {
            OutlookCategoriesJson = "[\"Karolina Wróbel\"]",
            OutlookCategoryColorsJson = "[{\"Name\":\"Karolina Wróbel\",\"Color\":null},{\"Name\":\"Inna\",\"Color\":null}]",
            PrimaryOutlookCategory = "Karolina Wróbel",
            Trainer = new Trainer { OutlookCategoryName = "Karolina Wróbel", OutlookCategoryColor = "preset8" }
        };
        var result = OutlookCategoryPresentation.Read(session);
        Assert.Equal("preset8", result.PrimaryColor);
        Assert.Null(result.Colors.Single(c => c.Name == "Inna").Color);
    }
}
