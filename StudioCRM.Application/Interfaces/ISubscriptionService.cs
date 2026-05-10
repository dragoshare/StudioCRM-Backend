using StudioCRM.Application.DTOs.Subscriptions;
using StudioCRM.Application.DTOs.TrainingPlans;

namespace StudioCRM.Application.Interfaces;

public interface ISubscriptionService
{
    Task<SubscriptionDto> GetCurrentClientSubscriptionAsync();
    Task<SubscriptionDto> GetClientSubscriptionAsync(int clientId);
    Task<SubscriptionDto> SetNextPackageAsync(int clientId, SetNextPackageRequest request);
    Task<SubscriptionDto> CancelRenewalAsync(int clientId);
    Task<SubscriptionDto> ResumeRenewalAsync(int clientId);
    Task<SubscriptionDto> RequestCancelRenewalAsClientAsync();
    Task<SubscriptionDto> WithdrawCancelRenewalRequestAsClientAsync();
    Task<SubscriptionUsageDto> GetCurrentClientUsageAsync();
    Task<SubscriptionUsageDto> GetClientUsageAsync(int clientId);
    Task<TrainingPlanDto> GetCurrentClientTrainingPlanAsync();
    Task<TrainingPlanDto> GetClientTrainingPlanAsync(int clientId);
    Task<TrainingPlanDto> UpdateTrainingPlanAsync(int clientId, UpdateTrainingPlanRequest request);
    Task RenewAfterCompletedCycleAsync(int clientPackageId);
}
