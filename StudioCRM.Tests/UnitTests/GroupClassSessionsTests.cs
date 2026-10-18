using StudioCRM.Application.Common;

namespace StudioCRM.Tests.UnitTests;

public class GroupClassSessionsTests
{
    [Theory]
    [InlineData("[\"Zajęcia grupowe\"]", true)]
    [InlineData("[\"Karolina Wróbel\",\" zajęcia GRUPOWE \"]", true)]
    [InlineData("[\"Zajecia grupowe\"]", true)]
    [InlineData("[\"Konsultacje\"]", false)]
    [InlineData("[]", false)]
    [InlineData(null, false)]
    [InlineData("not json", false)]
    public void RecognizesTheGroupClassCategory(string? categoriesJson, bool expected) =>
        Assert.Equal(expected, GroupClassSessions.HasOutlookCategory(categoriesJson));
}
