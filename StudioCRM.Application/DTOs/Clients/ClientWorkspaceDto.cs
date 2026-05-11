using StudioCRM.Application.DTOs.Billing;
using StudioCRM.Application.DTOs.Subscriptions;
using StudioCRM.Application.DTOs.TrainingPlans;

namespace StudioCRM.Application.DTOs.Clients;

public class ClientWorkspaceDto
{
    public ClientDto Profile { get; set; } = new();
    public ClientWorkspaceTrainerDto? Trainer { get; set; }
    public SubscriptionDto? Subscription { get; set; }
    public ClientBillingSummaryDto? Billing { get; set; }
    public TrainingPlanDto? TrainingPlan { get; set; }
    public List<ClientWorkspaceSessionDto> UpcomingSessions { get; set; } = new();
    public List<ClientWorkspaceCountedSessionDto> CountedSessions { get; set; } = new();
    public ClientWorkspaceQuickActionsDto QuickActions { get; set; } = new();
}

public class ClientWorkspaceTrainerDto
{
    public int TrainerId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string EmailContactUrl { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string? PhoneContactUrl { get; set; }
    public string? AvatarUrl { get; set; }
}

public class ClientWorkspaceSessionDto
{
    public int SessionId { get; set; }
    public string Title { get; set; } = string.Empty;
    public DateTime StartAt { get; set; }
    public DateTime EndAt { get; set; }
    public string Status { get; set; } = string.Empty;
    public string LocationName { get; set; } = string.Empty;
    public string TrainerFullName { get; set; } = string.Empty;
    public string AttendanceStatus { get; set; } = string.Empty;
    public bool CountsAgainstPackage { get; set; }
    public bool IsCountedFromPackage { get; set; }
}

public class ClientWorkspaceCountedSessionDto
{
    public int SessionId { get; set; }
    public DateTime Date { get; set; }
    public string TrainerFullName { get; set; } = string.Empty;
    public string LocationName { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public int SessionsCharged { get; set; }
    public string PlannedBillingType { get; set; } = string.Empty;
    public string ActualBillingType { get; set; } = string.Empty;
    public decimal ExpectedUnitPrice { get; set; }
    public decimal ActualUnitPrice { get; set; }
    public decimal BalanceDifference { get; set; }
}

public class ClientWorkspaceQuickActionsDto
{
    public bool CanChangePackage { get; set; } = true;
    public bool CanChangeTrainer { get; set; } = true;
    public bool CanDeactivate { get; set; } = true;
    public bool CanAddPayment { get; set; } = true;
    public string? GoogleDriveFolderUrl { get; set; }
    public string? TrainingPlanUrl { get; set; }
}
