using StudioCRM.Application.Common;
using StudioCRM.Domain.Enums;

namespace StudioCRM.Tests.UnitTests;

public class PackageTypeMapperTests
{
    [Theory]
    [InlineData(SessionBillingType.Group, 1, "Group")]
    [InlineData(SessionBillingType.Group, 4, "Group")]
    [InlineData(SessionBillingType.OneToOne, 1, "Individual")]
    [InlineData(SessionBillingType.FourToOne, 4, "SemiPersonal")]
    public void PackageResponseExposesTypeIndependentlyOfParticipantCount(SessionBillingType billingType, int count, string expected)
    {
        var dto = new StudioCRM.Application.DTOs.Packages.PackageDto { BillingType = billingType, ParticipantsCount = count };
        using var json = System.Text.Json.JsonDocument.Parse(System.Text.Json.JsonSerializer.Serialize(dto,
            new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web)));
        Assert.Equal(expected, json.RootElement.GetProperty("packageType").GetString());
        Assert.Equal(count, json.RootElement.GetProperty("participantsCount").GetInt32());
    }

    [Theory]
    [InlineData(SessionBillingType.OneToOne, "Individual")]
    [InlineData(SessionBillingType.TwoToOne, "SemiPersonal")]
    [InlineData(SessionBillingType.ThreeToOne, "SemiPersonal")]
    [InlineData(SessionBillingType.FourToOne, "SemiPersonal")]
    [InlineData(SessionBillingType.Group, "Group")]
    [InlineData((SessionBillingType)0, "Unknown")]
    public void ClassifiesBillingTypes(SessionBillingType billingType, string expected)
        => Assert.Equal(expected, PackageTypeMapper.FromBillingType(billingType));
}
