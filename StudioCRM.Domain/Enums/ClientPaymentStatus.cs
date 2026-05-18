namespace StudioCRM.Domain.Enums;

public enum ClientPaymentStatus
{
    PendingConfirmation = 1,
    Confirmed = 2,
    Rejected = 3,
    Cancelled = 4,
    Reversed = 5
}
