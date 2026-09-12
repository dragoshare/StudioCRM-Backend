using StudioCRM.Application.Common;

namespace StudioCRM.Tests.UnitTests;

public class NotificationCategoryMappingTests
{
    [Theory]
    [InlineData("GroupClassBooked", "group_classes")]
    [InlineData("GroupClassBookingCancelled", "group_classes")]
    [InlineData("GroupClassCancelled", "group_classes")]
    [InlineData("GroupClassRescheduled", "group_classes")]
    [InlineData("GroupClassReminder", "group_classes")]
    [InlineData("GroupPackagePaymentRequired", "payments")]
    [InlineData("GroupPackagePaymentConfirmed", "payments")]
    [InlineData("GroupPackageActivated", "packages")]
    [InlineData("PublicGroupClientRegistered", "registrations")]
    public void ResolvesGroupNotificationCategory(string type, string expectedCategory)
    {
        Assert.Equal(expectedCategory, NotificationCategories.Resolve(type));
    }
}
