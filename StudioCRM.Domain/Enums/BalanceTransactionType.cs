namespace StudioCRM.Domain.Enums;

public enum BalanceTransactionType
{
    PackageAdjustment = 1,
    ManualAdjustment = 2,
    PaymentCredit = 3,
    UsedInNextPackage = 4,
    PaymentOverpayment = 5,
    PaymentReversal = 6
}
