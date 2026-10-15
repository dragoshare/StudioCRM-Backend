using StudioCRM.Api.BackgroundServices;

namespace StudioCRM.Tests.UnitTests;

public class OutlookWebhookQueueTests
{
    [Fact]
    public void QueueAcceptsNotificationsUntilFullThenRefuses()
    {
        var queue = new OutlookWebhookQueue();
        for (var i = 0; i < 500; i++)
            Assert.True(queue.TryEnqueue("{\"value\":[]}"));
        Assert.False(queue.TryEnqueue("{\"value\":[]}"));
    }
}
