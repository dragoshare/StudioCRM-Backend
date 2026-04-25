namespace StudioCRM.Application.Interfaces.Calendar;

public interface IOutlookSubscriptionService
{
    Task CreateSubscriptionAsync();

    Task RenewExpiringSubscriptionsAsync();
}