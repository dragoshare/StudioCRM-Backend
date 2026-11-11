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
            OutlookCategoriesJson = "[\"Anna Nowak\"]",
            Trainer = new Trainer { OutlookCategoryName = "Anna Nowak", OutlookCategoryColor = "preset9" }
        };
        var result = OutlookCategoryPresentation.Read(session);
        Assert.Equal("Anna Nowak", result.Primary);
        Assert.Equal("preset9", result.PrimaryColor);
    }

    [Fact]
    public void StoredColourWinsAndMissingTrainerIsTolerated()
    {
        var session = new Session
        {
            OutlookCategoriesJson = "[\"Jan Kowalski\",\"Konsultacje\"]",
            OutlookCategoryColorsJson = "[{\"Name\":\"Jan Kowalski\",\"Color\":\"preset7\"}]",
            PrimaryOutlookCategory = "Jan Kowalski"
        };
        var result = OutlookCategoryPresentation.Read(session);
        Assert.Equal("preset7", result.PrimaryColor);
        Assert.Equal(2, result.Colors.Count);
        Assert.Null(result.Colors.Single(c => c.Name == "Konsultacje").Color);
    }

    // The trainer category stored from Outlook with a different or missing colour still shows the CRM trainer colour.
    [Theory]
    [InlineData("\"preset11\"")]
    [InlineData("null")]
    public void TrainerCategoryAlwaysUsesCrmTrainerColour(string storedColor)
    {
        var session = new Session
        {
            OutlookCategoriesJson = "[\"Ewa Król\",\"Konsultacje\"]",
            OutlookCategoryColorsJson = $"[{{\"Name\":\"Ewa Król\",\"Color\":{storedColor}}},{{\"Name\":\"Konsultacje\",\"Color\":\"preset3\"}}]",
            PrimaryOutlookCategory = "Ewa Król",
            Trainer = new Trainer { OutlookCategoryName = "Ewa Król", OutlookCategoryColor = "preset10" }
        };
        var result = OutlookCategoryPresentation.Read(session);
        Assert.Equal("preset10", result.PrimaryColor);
        Assert.Equal("preset3", result.Colors.Single(c => c.Name == "Konsultacje").Color);
    }
}
